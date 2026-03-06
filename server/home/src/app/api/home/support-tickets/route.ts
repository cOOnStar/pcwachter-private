import { NextResponse } from "next/server";
import { auth } from "@/auth";
import { normalizeSupportTickets } from "@/lib/api";

const API_URL =
  process.env.API_INTERNAL_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "https://api.xn--pcwchter-2za.de";

export async function GET() {
  const session = await auth();
  if (!session?.accessToken) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const res = await fetch(`${API_URL}/api/v1/support/tickets?per_page=50`, {
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
    },
    cache: "no-store",
  });

  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    return NextResponse.json(
      { error: (data as { detail?: string }).detail ?? "support_tickets_fetch_failed" },
      { status: res.status }
    );
  }

  return NextResponse.json({
    items: normalizeSupportTickets(data),
  });
}
