import { NextRequest, NextResponse } from "next/server";
import { auth } from "@/auth";

const API_URL =
  process.env.API_INTERNAL_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "https://api.xn--pcwchter-2za.de";

function unauthorized() {
  return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
}

export async function GET() {
  const session = await auth();
  if (!session?.accessToken) {
    return unauthorized();
  }

  const response = await fetch(`${API_URL}/api/v1/me/profile`, {
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
    },
    cache: "no-store",
  });

  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const detail = (data as { detail?: string }).detail ?? "profile_fetch_failed";
    return NextResponse.json({ error: detail }, { status: response.status });
  }

  return NextResponse.json(data);
}

export async function PATCH(req: NextRequest) {
  const session = await auth();
  if (!session?.accessToken) {
    return unauthorized();
  }

  const body = await req.json().catch(() => ({}));
  const email = typeof body.email === "string" ? body.email.trim() : "";
  const firstName = typeof body.first_name === "string" ? body.first_name : "";
  const lastName = typeof body.last_name === "string" ? body.last_name : "";

  if (!email) {
    return NextResponse.json({ error: "email_required" }, { status: 400 });
  }

  const response = await fetch(`${API_URL}/api/v1/me/profile`, {
    method: "PATCH",
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      email,
      first_name: firstName,
      last_name: lastName,
    }),
  });

  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const detail = (data as { detail?: string }).detail ?? "profile_update_failed";
    return NextResponse.json({ error: detail }, { status: response.status });
  }

  return NextResponse.json(data);
}
