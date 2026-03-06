import { Link } from "react-router";
import { Bell, User, LogOut, Key, HelpCircle, FileText, Menu } from "lucide-react";
import { useAuth } from "../../../hooks";
import { usePortalBootstrap } from "../../../hooks";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";

interface HeaderProps {
  onMenuClick: () => void;
}

export function Header({ onMenuClick }: HeaderProps) {
  const { user: authUser, logout } = useAuth();
  const { data } = usePortalBootstrap();
  const user = data?.user ?? authUser;
  const notifications = data?.notifications ?? [];
  const unreadCount = notifications.filter((n) => !n.read).length;

  const formatRelativeTime = (dateStr: string) => {
    const now = new Date();
    const date = new Date(dateStr);
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'gerade eben';
    if (diffMins < 60) return `vor ${diffMins} Min.`;
    if (diffHours < 24) return `vor ${diffHours} Std.`;
    return `vor ${diffDays} Tag${diffDays > 1 ? 'en' : ''}`;
  };

  const getNotificationColor = (type: string) => {
    switch (type) {
      case "info":
        return "bg-blue-500";
      case "warning":
        return "bg-yellow-500";
      case "success":
        return "bg-green-500";
      default:
        return "bg-gray-300";
    }
  };

  return (
    <header className="h-16 bg-white border-b border-gray-200 flex items-center justify-between px-4 lg:px-6">
      {/* Left side with burger menu */}
      <div className="flex items-center gap-4">
        <button
          onClick={onMenuClick}
          className="p-2 hover:bg-gray-100 rounded-lg transition-colors md:hidden"
        >
          <Menu className="w-6 h-6 text-gray-700" />
        </button>
        
        <div>
          <h2 className="text-base sm:text-lg lg:text-xl font-semibold text-gray-800 truncate max-w-[200px] sm:max-w-none">
            {user?.firstName ? `Willkommen zurück, ${user.firstName}!` : 'Willkommen zurück!'}
          </h2>
        </div>
      </div>

      <div className="flex items-center gap-2 lg:gap-4">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button className="relative p-2 hover:bg-gray-100 rounded-lg transition-colors">
              <Bell className="w-5 h-5 text-gray-700" />
              {unreadCount > 0 && (
                <span className="absolute top-1.5 right-1.5 w-2 h-2 bg-red-500 rounded-full"></span>
              )}
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-80">
            <DropdownMenuLabel>Benachrichtigungen</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <div className="max-h-96 overflow-y-auto">
              {notifications.slice(0, 3).map((notification) => (
                <div 
                  key={notification.id}
                  className="p-3 hover:bg-gray-50 cursor-pointer border-b last:border-b-0"
                >
                  <div className="flex items-start gap-3">
                    <div className={`w-2 h-2 ${getNotificationColor(notification.type)} rounded-full mt-2 flex-shrink-0`}></div>
                    <div className="flex-1">
                      <p className="text-sm font-medium">{notification.title}</p>
                      <p className="text-xs text-gray-600 mt-1">{notification.message}</p>
                      <p className="text-xs text-gray-400 mt-1">{formatRelativeTime(notification.timestamp)}</p>
                    </div>
                  </div>
                </div>
              ))}
            </div>
            <DropdownMenuSeparator />
            <div className="p-2">
              <Link
                to="/notifications"
                className="block text-center text-sm text-blue-600 hover:text-blue-700 font-medium py-2"
              >
                Alle Benachrichtigungen anzeigen
              </Link>
            </div>
          </DropdownMenuContent>
        </DropdownMenu>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button className="flex items-center gap-2 hover:bg-gray-100 rounded-lg px-3 py-2 transition-colors">
              <div className="w-8 h-8 bg-blue-100 rounded-full flex items-center justify-center">
                <User className="w-5 h-5 text-blue-700" />
              </div>
              <div className="text-left hidden md:block">
                <div className="text-sm font-medium">
                  {user?.firstName || user?.username || 'Benutzer'} {user?.lastName || ''}
                </div>
                <div className="text-xs text-gray-500">{user?.email || ''}</div>
              </div>
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56">
            <DropdownMenuLabel>Mein Account</DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem asChild>
              <Link to="/profile" className="cursor-pointer flex items-center">
                <User className="w-4 h-4 mr-2" />
                Profil anzeigen
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem asChild>
              <Link to="/licenses" className="cursor-pointer flex items-center">
                <Key className="w-4 h-4 mr-2" />
                Meine Lizenzen
              </Link>
            </DropdownMenuItem>
            <DropdownMenuItem asChild>
              <Link to="/support" className="cursor-pointer flex items-center">
                <HelpCircle className="w-4 h-4 mr-2" />
                Support
              </Link>
            </DropdownMenuItem>
            <DropdownMenuItem asChild>
              <Link to="/documentation" className="cursor-pointer flex items-center">
                <FileText className="w-4 h-4 mr-2" />
                Dokumentation
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={logout} className="cursor-pointer text-red-600">
              <LogOut className="w-4 h-4 mr-2" />
              Abmelden
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  );
}
