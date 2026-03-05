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

  const origin = req.headers.get("origin") ?? "https://home.xn--pcwchter-2za.de";

  const apiRes = await fetch(`${API_URL}/payments/portal`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ return_url: `${origin}/account/billing` }),
  });

  const data = await apiRes.json().catch(() => ({}));
  if (!apiRes.ok) {
    const detail = (data as { detail?: string }).detail ?? "";
    if (apiRes.status === 404 && detail === "no stripe customer found") {
      return NextResponse.json({ error: "no_billing_account" }, { status: 404 });
    }
    return NextResponse.json(
      { error: detail || "Fehler beim Öffnen des Kundenportals." },
      { status: apiRes.status }
    );
  }

  return NextResponse.json(data, { status: 200 });
}
