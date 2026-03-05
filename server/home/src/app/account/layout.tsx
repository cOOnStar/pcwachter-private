import { auth } from "@/auth";
import { redirect } from "next/navigation";
import AccountNav from "./nav";

export default async function AccountLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const session = await auth();

  if (!session?.user || !session.accessToken) {
    redirect("/login?callbackUrl=%2Faccount");
  }

  return (
    <div style={{ padding: "3rem 0" }}>
      <div className="container">
        <div className="account-shell">
          <AccountNav />
          <div style={{ flex: 1, minWidth: 0 }}>{children}</div>
        </div>
      </div>
    </div>
  );
}
