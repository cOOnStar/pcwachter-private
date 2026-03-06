import { NextResponse } from "next/server";
import { getPlansResult } from "@/lib/api";

export async function GET() {
  const result = await getPlansResult();
  if (result.error) {
    return NextResponse.json(
      {
        items: result.items,
        error: "plans_unavailable",
      },
      { status: 502 }
    );
  }

  return NextResponse.json({ items: result.items });
}
