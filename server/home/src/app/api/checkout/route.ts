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
  const { plan_id } = body as { plan_id?: string };

  if (!plan_id) {
    return NextResponse.json({ error: "plan_id required" }, { status: 400 });
  }

  const origin = req.headers.get("origin") ?? "https://home.xn--pcwchter-2za.de";

  const apiRes = await fetch(`${API_URL}/payments/create-checkout`, {
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

  const data = await apiRes.json().catch(() => ({}));
  if (!apiRes.ok) {
    return NextResponse.json(
      { error: (data as { detail?: string }).detail ?? "Checkout fehlgeschlagen." },
      { status: apiRes.status }
    );
  }

  return NextResponse.json(data, { status: 200 });
}
