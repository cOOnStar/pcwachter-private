import { NextResponse } from "next/server";
import { getPlans } from "@/lib/api";

export async function GET() {
  const plans = await getPlans();
  return NextResponse.json({ items: plans });
}
