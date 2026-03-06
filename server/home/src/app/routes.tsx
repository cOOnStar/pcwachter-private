import { createBrowserRouter } from "react-router";
import { Root } from "./components/layout/Root";
import { Dashboard } from "./pages/Dashboard";
import { Licenses } from "./pages/Licenses";
import { BuyLicense } from "./pages/BuyLicense";
import { Devices } from "./pages/Devices";
import { Support } from "./pages/Support";
import { Downloads } from "./pages/Downloads";
import { Documentation } from "./pages/Documentation";
import { Profile } from "./pages/Profile";
import { Notifications } from "./pages/Notifications";
import { NotFound } from "./pages/NotFound";
import AuthCallback from "./pages/AuthCallback";
import { ProtectedLayout } from "./components/layout/ProtectedLayout";

export const router = createBrowserRouter([
  {
    path: "/auth/callback",
    Component: AuthCallback,
  },
  {
    path: "/",
    Component: ProtectedLayout,
    children: [
      {
        path: "/",
        Component: Root,
        children: [
          { index: true, Component: Dashboard },
          { path: "dashboard", Component: Dashboard },
          { path: "licenses", Component: Licenses },
          { path: "licenses/buy", Component: BuyLicense },
          { path: "devices", Component: Devices },
          { path: "support", Component: Support },
          { path: "downloads", Component: Downloads },
          { path: "documentation", Component: Documentation },
          { path: "notifications", Component: Notifications },
          { path: "profile", Component: Profile },
          { path: "*", Component: NotFound },
        ],
      },
    ],
  },
]);