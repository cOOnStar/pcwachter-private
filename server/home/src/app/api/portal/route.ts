import { NextRequest, NextResponse } from "next/server";
import { auth } from "@/auth";

const API_URL =
  process.env.API_INTERNAL_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "https://api.xn--pcwchter-2za.de";

function apiV1Base(): string {
  const base = API_URL.replace(/\/+$/, "");
  return base.endsWith("/api/v1") ? base : `${base}/api/v1`;
}

async function readApiPayload(res: Response): Promise<{ detail?: string; error?: string; portal_url?: string }> {
  const text = await res.text().catch(() => "");
  if (!text) return {};
  try {
    return JSON.parse(text) as { detail?: string; error?: string; portal_url?: string };
  } catch {
    return { error: text };
  }
}

export async function POST(req: NextRequest) {
  const session = await auth();
  if (!session?.accessToken) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const origin = req.nextUrl.origin;

  const apiRes = await fetch(`${apiV1Base()}/payments/portal`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ return_url: `${origin}/account/billing` }),
  });

  const data = await readApiPayload(apiRes);
  if (!apiRes.ok) {
    const detail = data.detail ?? data.error ?? "";
    if (apiRes.status === 404 && detail === "no stripe customer found") {
      return NextResponse.json({ error: "no_billing_account" }, { status: 404 });
    }
    return NextResponse.json(
      { error: detail || "Fehler beim Öffnen des Kundenportals.", status: apiRes.status },
      { status: apiRes.status }
    );
  }

  return NextResponse.json({ portal_url: data.portal_url }, { status: 200 });
}
