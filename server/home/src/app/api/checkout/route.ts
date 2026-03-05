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

async function readErrorMessage(res: Response): Promise<string> {
  const text = await res.text().catch(() => "");
  if (!text) return "Checkout fehlgeschlagen.";
  try {
    const body = JSON.parse(text) as { detail?: string; error?: string };
    if (typeof body.detail === "string" && body.detail.trim()) return body.detail;
    if (typeof body.error === "string" && body.error.trim()) return body.error;
  } catch {
    // Fall back to plain text.
  }
  return text;
}

export async function POST(req: NextRequest) {
  const session = await auth();
  if (!session?.accessToken) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const body = await req.json().catch(() => ({}));
  const { plan_id } = body as { plan_id?: string };

  if (!plan_id) {
    return NextResponse.json({ error: "plan_id required" }, { status: 400 });
  }

  const origin = req.nextUrl.origin;

  const apiRes = await fetch(`${apiV1Base()}/payments/create-checkout`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      plan_id,
      success_url: `${origin}/account/billing?checkout=success`,
      cancel_url: `${origin}/account/billing?checkout=cancel`,
    }),
  });

  if (!apiRes.ok) {
    const message = await readErrorMessage(apiRes);
    return NextResponse.json(
      { error: message, status: apiRes.status },
      { status: apiRes.status }
    );
  }

  const data = await apiRes.json().catch(() => ({})) as { checkout_url?: string };
  return NextResponse.json({ checkout_url: data.checkout_url }, { status: 200 });
}
