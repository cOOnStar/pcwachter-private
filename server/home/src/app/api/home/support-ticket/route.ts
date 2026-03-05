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

  const body = await req.json().catch(() => ({}));
  const { title, body: ticketBody } = body as { title?: string; body?: string };

  if (!title?.trim() || !ticketBody?.trim()) {
    return NextResponse.json({ error: "title and body required" }, { status: 400 });
  }

  const res = await fetch(`${API_URL}/support/tickets`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ title: title.trim(), body: ticketBody.trim() }),
  });

  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    const detail = (data as { detail?: string }).detail ?? "Fehler beim Erstellen";
    const isNotConfigured = res.status === 503 || String(detail).includes("support_not_configured");
    return NextResponse.json(
      { error: isNotConfigured ? "support_not_configured" : detail },
      { status: res.status }
    );
  }
  return NextResponse.json(data);
}
