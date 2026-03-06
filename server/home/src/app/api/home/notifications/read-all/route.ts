import { NextRequest, NextResponse } from "next/server";
import { auth } from "@/auth";

const API_URL =
  process.env.API_INTERNAL_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "https://api.xn--pcwchter-2za.de";

export async function POST(req: NextRequest) {
  const session = await auth();
  if (!session?.accessToken) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const upstreamUrl = new URL(`${API_URL}/api/v1/notifications/read-all`);
  const typePrefix = req.nextUrl.searchParams.get("type_prefix");
  if (typePrefix) {
    upstreamUrl.searchParams.set("type_prefix", typePrefix);
  }

  const res = await fetch(upstreamUrl.toString(), {
    method: "POST",
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
    },
  });

  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    return NextResponse.json(
      { error: (data as { detail?: string }).detail ?? "notifications_mark_read_failed" },
      { status: res.status }
    );
  }

  return NextResponse.json(data);
}
