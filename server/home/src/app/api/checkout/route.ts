import { NextRequest, NextResponse } from "next/server";
import { auth } from "@/auth";
import { stripe } from "@/lib/stripe";
import { getPlans } from "@/lib/api";
import type Stripe from "stripe";

export async function POST(req: NextRequest) {
  if (!process.env.STRIPE_SECRET_KEY) {
    return NextResponse.json({ error: "Stripe not configured yet" }, { status: 503 });
  }

  const session = await auth();
  if (!session?.user || !session.userId) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const body = await req.json().catch(() => ({}));
  const { plan_id } = body as { plan_id?: string };

  if (!plan_id) {
    return NextResponse.json({ error: "plan_id required" }, { status: 400 });
  }

  const plans = await getPlans();
  const plan = plans.find((p) => p.id === plan_id);

  if (!plan) {
    return NextResponse.json({ error: "Plan not found" }, { status: 404 });
  }

  const isSubscription = plan.duration_days === 30 || plan.duration_days === 365;
  const hasNumericPrice = typeof plan.price_eur === "number" && plan.price_eur > 0;

  if (!plan.stripe_price_id && !hasNumericPrice) {
    return NextResponse.json({ error: "Plan not found or not purchasable" }, { status: 404 });
  }

  const origin = req.headers.get("origin") ?? "https://home.xn--pcwchter-2za.de";
  const lineItems: Stripe.Checkout.SessionCreateParams.LineItem[] = [];
  if (plan.stripe_price_id) {
    lineItems.push({ price: plan.stripe_price_id, quantity: 1 });
  } else {
    const priceData: Stripe.Checkout.SessionCreateParams.LineItem.PriceData = {
      currency: "eur",
      unit_amount: Math.round((plan.price_eur ?? 0) * 100),
      product_data: { name: `PCWächter ${plan.label}` },
    };
    if (isSubscription) {
      priceData.recurring = {
        interval: plan.duration_days === 365 ? "year" : "month",
      };
    }
    lineItems.push({ price_data: priceData, quantity: 1 });
  }

  const checkoutSession = await stripe.checkout.sessions.create({
    mode: isSubscription ? "subscription" : "payment",
    line_items: lineItems,
    customer_email: session.user.email ?? undefined,
    metadata: {
      keycloak_user_id: session.userId,
      plan_id,
    },
    success_url: `${origin}/account?success=1`,
    cancel_url: `${origin}/account/billing?cancelled=1`,
  });

  return NextResponse.json({ checkout_url: checkoutSession.url });
}
