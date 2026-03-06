import { Link, useLocation } from "react-router";
import { 
  LayoutDashboard, 
  Key, 
  Monitor,
  Headphones, 
  Download, 
  BookOpen,
  X
} from "lucide-react";
import { useGitHubRelease } from "../../../hooks";
import logo from "../../../assets/57f6784516073fdb034a58c324631d2ffc263593.png";

const navigation = [
  { name: "Dashboard", href: "/", icon: LayoutDashboard },
  { name: "Lizenzen", href: "/licenses", icon: Key },
  { name: "Meine Geräte", href: "/devices", icon: Monitor },
  { name: "Support", href: "/support", icon: Headphones },
  { name: "Downloads", href: "/downloads", icon: Download },
  { name: "Dokumentation", href: "/documentation", icon: BookOpen },
];

interface SidebarProps {
  isOpen: boolean;
  onClose: () => void;
}

export function Sidebar({ isOpen, onClose }: SidebarProps) {
  // Safe guard for HMR - handle case when router context is not available
  let location;
  try {
    location = useLocation();
  } catch (e) {
    // Fallback during HMR when router context might not be available
    location = { pathname: "/" };
  }
  
  const { release, loading } = useGitHubRelease();
  const version = loading ? "..." : (release?.tag_name || "2.5.1");

  return (
    <>
      {/* Mobile Overlay */}
      {isOpen && (
        <div 
          className="fixed inset-0 bg-black/50 z-40 md:hidden"
          onClick={onClose}
        />
      )}

      {/* Sidebar */}
      <aside className={`
        fixed md:static inset-y-0 left-0 z-50
        w-64 bg-white border-r border-gray-200 flex flex-col
        transform transition-transform duration-300 ease-in-out
        ${isOpen ? 'translate-x-0' : '-translate-x-full md:translate-x-0'}
      `}>
        {/* Close button for mobile */}
        <button
          onClick={onClose}
          className="absolute top-4 right-4 p-2 rounded-lg hover:bg-gray-100 md:hidden"
        >
          <X className="w-5 h-5 text-gray-600" />
        </button>

        <div className="p-6 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <img src={logo} alt="PC-Wächter Logo" className="w-10 h-10" />
            <div>
              <h1 className="font-semibold text-lg">PC-Wächter</h1>
              <p className="text-sm text-gray-500">Kundenportal</p>
            </div>
          </div>
        </div>
        
        <nav className="flex-1 p-4 space-y-1">
          {navigation.map((item) => {
            const isActive = location.pathname === item.href;
            const Icon = item.icon;
            
            return (
              <Link
                key={item.name}
                to={item.href}
                onClick={onClose}
                className={`
                  flex items-center gap-3 px-4 py-3 rounded-lg transition-colors
                  ${isActive 
                    ? "bg-blue-50 text-blue-700" 
                    : "text-gray-700 hover:bg-gray-50"
                  }
                `}
              >
                <Icon className="w-5 h-5" />
                <span className="font-medium">{item.name}</span>
              </Link>
            );
          })}
        </nav>

        <div className="p-4 border-t border-gray-200">
          <div className="text-xs text-gray-500 text-center">
            Version {version} • © 2026 PC-Wächter
          </div>
        </div>
      </aside>
    </>
  );
}
