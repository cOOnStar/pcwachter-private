import { NextRequest, NextResponse } from "next/server";
import { auth } from "@/auth";

const API_URL =
  process.env.API_INTERNAL_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "https://api.xn--pcwchter-2za.de";

export async function GET(req: NextRequest) {
  const session = await auth();
  if (!session?.accessToken) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const upstreamUrl = new URL(`${API_URL}/api/v1/notifications`);
  const unreadOnly = req.nextUrl.searchParams.get("unread_only");
  const limit = req.nextUrl.searchParams.get("limit");
  const typePrefix = req.nextUrl.searchParams.get("type_prefix");

  if (unreadOnly) {
    upstreamUrl.searchParams.set("unread_only", unreadOnly);
  }
  if (limit) {
    upstreamUrl.searchParams.set("limit", limit);
  }
  if (typePrefix) {
    upstreamUrl.searchParams.set("type_prefix", typePrefix);
  }

  const res = await fetch(upstreamUrl.toString(), {
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
    },
    cache: "no-store",
  });

  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    return NextResponse.json(
      { error: (data as { detail?: string }).detail ?? "notifications_fetch_failed" },
      { status: res.status }
    );
  }

  return NextResponse.json(data);
}
