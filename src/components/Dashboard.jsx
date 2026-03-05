import React from 'react';
import { 
  LayoutDashboard, Shield, Windows, RefreshCw, HardDrive, 
  Network, History, User, Settings, HelpCircle, AlertCircle, 
  CheckCircle, ChevronRight, Volume2, Database, Laptop 
} from 'lucide-react';

const SidebarItem = ({ icon: Icon, label, active = false }) => (
  <div className={`flex items-center gap-3 px-4 py-3 cursor-pointer transition-all ${
    active ? 'bg-blue-600 text-white rounded-lg' : 'text-gray-400 hover:text-white hover:bg-gray-800'
  }`}>
    <Icon size={20} />
    <span className="text-sm font-medium">{label}</span>
  </div>
);

const HealthCard = ({ score, status }) => (
  <div className="bg-[#1a1d24] border border-gray-800 rounded-xl p-6 relative overflow-hidden">
    <h3 className="text-gray-400 text-sm mb-4">Gesundheitswert</h3>
    <div className="flex flex-col items-center">
      {/* Gauge Placeholder */}
      <div className="relative w-40 h-20 overflow-hidden">
        <div className="absolute top-0 w-40 h-40 border-[12px] border-gray-800 rounded-full" />
        <div 
          className="absolute top-0 w-40 h-40 border-[12px] border-t-orange-400 border-r-green-500 border-l-red-500 rounded-full" 
          style={{ transform: 'rotate(45deg)' }} 
        />
        <div className="absolute bottom-0 left-1/2 -translate-x-1/2 text-center">
          <span className="text-4xl font-bold text-white leading-none">{score}</span>
          <p className="text-sm text-gray-400 mt-1">{status}</p>
        </div>
      </div>
      
      <div className="flex gap-2 mt-8 w-full">
        <button className="flex-1 bg-blue-600 hover:bg-blue-700 text-white py-2 rounded-lg flex items-center justify-center gap-2 text-sm">
          <RefreshCw size={16} /> Jetzt scannen
        </button>
        <button className="flex-1 bg-gray-800 hover:bg-gray-700 text-white py-2 rounded-lg flex items-center justify-center gap-2 text-sm border border-gray-700">
          <User size={16} /> Auto-Fix Einstellungen
        </button>
      </div>
    </div>
  </div>
);

const ProblemCard = ({ icon: Icon, title, subtitle, color, badge }) => (
  <div className="bg-[#1a1d24] border border-gray-800 rounded-xl p-4 flex-1">
    <div className="flex items-start gap-4">
      <div className={`p-2 rounded-lg ${color} bg-opacity-20`}>
        <Icon className={color.replace('bg-', 'text-')} size={24} />
      </div>
      <div>
        <h4 className="text-white text-sm font-medium leading-tight">{title}</h4>
        <p className="text-gray-500 text-xs mt-1 flex items-center gap-1">
           {subtitle} {badge && <span className="bg-red-600 text-[10px] px-1 rounded ml-1">NEU</span>}
        </p>
      </div>
    </div>
  </div>
);

const Dashboard = () => {
  return (
    <div className="flex h-screen bg-[#0d1117] text-white font-sans overflow-hidden">
      {/* Main Content (Links) */}
      <div className="flex-1 flex flex-col overflow-hidden">
        
        {/* Header Banner */}
        <div className="h-48 bg-gradient-to-r from-blue-700 to-blue-500 p-8 flex justify-between items-center relative">
          <div>
            <h1 className="text-3xl font-bold">Willkommen beim PCWächter</h1>
            <p className="text-blue-100 mt-2 flex items-center gap-2">
              <Shield size={18} /> Ihr PC wird überwacht und geschützt.
            </p>
          </div>
          <div className="relative">
            <img src="/shield-laptop-mockup.png" alt="Status" className="w-56 drop-shadow-2xl" />
          </div>
        </div>

        {/* Dashboard Grid */}
        <div className="p-6 flex flex-col gap-6 flex-1 overflow-y-auto">
          <div className="grid grid-cols-12 gap-6">
            
            {/* Left Column (Main Stats) */}
            <div className="col-span-8 flex flex-col gap-6">
              <HealthCard score="82" status="Gut" />
              
              <div className="space-y-4">
                <h3 className="text-sm font-medium text-gray-400">Top 3 Probleme</h3>
                <div className="flex gap-4">
                  <ProblemCard icon={CheckCircle} title="Veraltete Software" subtitle="Seit 5 Tagen" color="bg-orange-500" />
                  <ProblemCard icon={Shield} title="Windows Defender deaktiviert" subtitle="" color="bg-cyan-500" badge />
                  <ProblemCard icon={AlertCircle} title="Speicherplatz knapp" subtitle="Gerade behoben" color="bg-red-500" />
                </div>
              </div>

              <div className="bg-[#1a1d24] border border-gray-800 rounded-xl overflow-hidden">
                 <div className="p-4 border-b border-gray-800"><h3 className="text-sm">Alle Probleme</h3></div>
                 <div className="p-2 space-y-1">
                    {[
                      { icon: Laptop, label: 'Treiber veraltet', time: 'Seit 2 Tagen' },
                      { icon: Shield, label: 'Firewall deaktiviert', critical: true },
                      { icon: CheckCircle, label: 'Registry-Fehler' }
                    ].map((item, i) => (
                      <div key={i} className="flex justify-between items-center p-3 hover:bg-white/5 rounded-lg cursor-pointer group">
                        <div className="flex items-center gap-3">
                          <item.icon size={18} className="text-gray-500" />
                          <span className="text-sm">{item.label}</span>
                        </div>
                        <div className="flex items-center gap-3">
                          {item.time && <span className="text-xs text-gray-500">{item.time}</span>}
                          {item.critical && <span className="bg-red-600/20 text-red-500 text-[10px] px-2 py-0.5 rounded border border-red-500/30">Kritisch</span>}
                          <ChevronRight size={16} className="text-gray-600 group-hover:text-white" />
                        </div>
                      </div>
                    ))}
                 </div>
              </div>
            </div>

            {/* Middle Column (Status) */}
            <div className="col-span-4 flex flex-col gap-6">
              <div className="bg-[#1a1d24] border border-gray-800 rounded-xl p-4">
                <h3 className="text-sm mb-4">Systemübersicht</h3>
                <div className="space-y-4">
                  <div className="flex items-center gap-3 text-sm text-gray-300">
                    <Laptop size={16} /> Gerät: HP EliteBook 850 G7
                  </div>
                  <div className="flex items-center gap-3 text-sm text-gray-300">
                    <History size={16} /> Letzter Scan: Vor 10 Minuten
                  </div>
                  <div className="flex items-center gap-3 text-sm text-gray-300">
                    <CheckCircle size={16} className="text-green-500" /> Zuletzt behoben: 3 Probleme
                  </div>
                </div>
              </div>

              <div className="bg-[#1a1d24] border border-gray-800 rounded-xl p-4">
                <h3 className="text-sm mb-4">Kürzlich behoben</h3>
                <div className="space-y-4">
                  <div className="flex items-center gap-3 text-sm text-gray-400">
                    <Database size={16} /> Temporäre Dateien gelöscht
                  </div>
                  <div className="flex items-center gap-3 text-sm text-gray-400">
                    <Shield size={16} /> Defender aktiviert
                  </div>
                  <div className="flex items-center gap-3 text-sm text-gray-400">
                    <Windows size={16} /> Windows Update installiert
                  </div>
                </div>
              </div>
            </div>

          </div>
        </div>

        {/* Status Bar */}
        <div className="h-10 bg-[#0d1117] border-t border-gray-800 px-4 flex items-center justify-between text-[11px] text-gray-500">
          <div className="flex gap-4">
            <span>Letzter Scan: <b>Heute</b></span>
            <span>v1.00</span>
            <span className="flex items-center gap-1"><div className="w-1.5 h-1.5 bg-green-500 rounded-full" /> Service verbunden</span>
          </div>
          <div className="flex items-center gap-4">
            <Volume2 size={14} />
            <History size={14} />
            <Settings size={14} />
            <div className="flex gap-0.5">
               <div className="h-3 w-1 bg-gray-600" />
               <div className="h-3 w-1 bg-gray-500" />
               <div className="h-3 w-1 bg-gray-400" />
            </div>
          </div>
        </div>
      </div>

      {/* Sidebar (Rechts) */}
      <div className="w-64 bg-[#161b22] border-l border-gray-800 p-4 space-y-2">
        <SidebarItem icon={LayoutDashboard} label="Dashboard" active />
        <SidebarItem icon={Shield} label="Sicherheit" />
        <SidebarItem icon={Windows} label="Windows" />
        <SidebarItem icon={RefreshCw} label="Windows Updates" />
        <SidebarItem icon={HardDrive} label="Speicher" />
        <SidebarItem icon={Network} label="Netzwerk" />
        <SidebarItem icon={History} label="Verlauf / Historie" />
        <SidebarItem icon={User} label="PCWächter Konto" />
        <div className="pt-4 mt-4 border-t border-gray-800">
          <SidebarItem icon={Settings} label="Optionen" />
          <SidebarItem icon={HelpCircle} label="Hilfe" />
        </div>
      </div>
    </div>
  );
};

export default Dashboard;