import { NextRequest, NextResponse } from "next/server";
import { auth } from "@/auth";
import { stripe } from "@/lib/stripe";

export async function POST(req: NextRequest) {
  if (!process.env.STRIPE_SECRET_KEY) {
    return NextResponse.json({ error: "Stripe not configured yet" }, { status: 503 });
  }

  const session = await auth();
  if (!session?.user) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const origin = req.headers.get("origin") ?? "https://home.xn--pcwchter-2za.de";

  // Find existing Stripe customer by email
  const customers = await stripe.customers.list({
    email: session.user.email ?? "",
    limit: 1,
  });

  if (customers.data.length === 0) {
    return NextResponse.json({ error: "no_billing_account" }, { status: 404 });
  }

  const portalSession = await stripe.billingPortal.sessions.create({
    customer: customers.data[0].id,
    return_url: `${origin}/account/billing`,
  });

  return NextResponse.json({ portal_url: portalSession.url });
}
