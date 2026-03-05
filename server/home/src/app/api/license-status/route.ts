import { NextResponse } from "next/server";
import { auth } from "@/auth";
import { getLicenseStatus } from "@/lib/api";

export async function GET() {
  const session = await auth();
  if (!session?.accessToken) {
    return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
  }

  const status = await getLicenseStatus(session.accessToken);
  if (!status) {
    return NextResponse.json({ error: "Could not retrieve license status" }, { status: 502 });
  }

  return NextResponse.json(status);
}
