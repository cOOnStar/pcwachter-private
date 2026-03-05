import { NextResponse } from "next/server";

const API_URL =
  process.env.API_INTERNAL_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "https://api.xn--pcwchter-2za.de";

function apiV1Base(): string {
  const base = API_URL.replace(/\/+$/, "");
  return base.endsWith("/api/v1") ? base : `${base}/api/v1`;
}

export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const component = searchParams.get("component") ?? "desktop";
  const channel = searchParams.get("channel") ?? "stable";

  const res = await fetch(
    `${apiV1Base()}/updates/latest?component=${component}&channel=${channel}`,
    { next: { revalidate: 300 } }
  );

  if (!res.ok) {
    return NextResponse.json({ error: "not found" }, { status: res.status });
  }

  const data = await res.json();
  return NextResponse.json(data);
}
