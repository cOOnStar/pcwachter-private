import { signIn } from "@/auth";

function sanitizeCallbackUrl(input: string | null): string {
  if (!input || !input.startsWith("/")) {
    return "/account";
  }
  return input;
}

export async function GET(request: Request) {
  const url = new URL(request.url);
  const redirectTo = sanitizeCallbackUrl(url.searchParams.get("callbackUrl"));
  return signIn("keycloak", { redirectTo });
}
