import { Card, CardContent, CardHeader, CardTitle } from "../components/ui/card";
import { Button } from "../components/ui/button";
import { Badge } from "../components/ui/badge";
import { 
  Download, 
  Package, 
  FileText, 
  CheckCircle,
  Calendar,
  Github,
  ExternalLink,
  History
} from "lucide-react";
import { motion } from "motion/react";
import { Link } from "react-router";
import { useGitHubRelease } from "../../hooks";
import { toast } from "sonner";

export function Downloads() {
  const { release, loading, error } = useGitHubRelease();

  const handleDownload = (asset: { browser_download_url?: string; name: string }) => {
    if (asset?.browser_download_url) {
      window.open(asset.browser_download_url, '_blank');
      toast.success("Download gestartet", {
        description: `${asset.name} wird heruntergeladen...`,
      });
    }
  };

  const formatFileSize = (bytes: number) => {
    const mb = bytes / (1024 * 1024);
    return `${mb.toFixed(2)} MB`;
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('de-DE', {
      day: '2-digit',
      month: 'long',
      year: 'numeric',
    });
  };

  return (
    <div className="p-4 md:p-6 space-y-6 max-w-6xl mx-auto">
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
      >
        <div className="flex items-center gap-3 mb-2">
          <div className="p-3 bg-blue-50 rounded-lg">
            <Download className="w-8 h-8 text-blue-600" />
          </div>
          <div>
            <h1 className="text-2xl md:text-3xl font-bold text-gray-900">Downloads</h1>
            <p className="text-gray-600">PC-Wächter Software und Dokumentation</p>
          </div>
        </div>
      </motion.div>

      {loading && (
        <Card>
          <CardContent className="p-12">
            <div className="flex flex-col items-center justify-center">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mb-4"></div>
              <p className="text-gray-600">Lade Release-Informationen...</p>
            </div>
          </CardContent>
        </Card>
      )}

      {error && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="p-6">
            <div className="flex items-center gap-3">
              <div className="p-2 bg-red-100 rounded-lg">
                <Package className="w-6 h-6 text-red-600" />
              </div>
              <div>
                <p className="font-semibold text-red-900">Fehler beim Laden</p>
                <p className="text-sm text-red-700">Die Release-Informationen konnten nicht abgerufen werden.</p>
              </div>
            </div>
          </CardContent>
        </Card>
      )}

      {!loading && !error && release && (
        <>
          {/* Latest Release Card */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
          >
            <Card className="border-2 border-blue-200 bg-gradient-to-br from-blue-50 to-white">
              <CardHeader>
                <div className="flex items-start justify-between gap-4">
                  <div className="flex-1">
                    <div className="flex items-center gap-3 mb-2">
                      <Badge className="bg-green-600 text-white">
                        <CheckCircle className="w-3 h-3 mr-1" />
                        Neueste Version
                      </Badge>
                      <Badge variant="outline" className="bg-white">
                        {release.tag_name}
                      </Badge>
                    </div>
                    <CardTitle className="text-2xl mb-2">{release.name}</CardTitle>
                    <div className="flex flex-wrap items-center gap-4 text-sm text-gray-600">
                      <div className="flex items-center gap-2">
                        <Calendar className="w-4 h-4" />
                        Veröffentlicht am {formatDate(release.published_at)}
                      </div>
                      <div className="flex items-center gap-2">
                        <Download className="w-4 h-4" />
                        {release.assets.length} Datei{release.assets.length !== 1 ? 'en' : ''}
                      </div>
                    </div>
                  </div>
                  <a
                    href={release.html_url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="p-2 hover:bg-white rounded-lg transition-colors"
                  >
                    <Github className="w-6 h-6 text-gray-600" />
                  </a>
                </div>
              </CardHeader>
              <CardContent className="space-y-6">
                {/* Release Notes */}
                {release.body && (
                  <div>
                    <h3 className="font-semibold text-gray-900 mb-3 flex items-center gap-2">
                      <FileText className="w-5 h-5 text-blue-600" />
                      Release Notes
                    </h3>
                    <div className="bg-white p-4 rounded-lg border border-gray-200">
                      <pre className="whitespace-pre-wrap text-sm text-gray-700 font-sans">
                        {release.body}
                      </pre>
                    </div>
                  </div>
                )}

                {/* Download Files */}
                <div>
                  <h3 className="font-semibold text-gray-900 mb-3 flex items-center gap-2">
                    <Package className="w-5 h-5 text-blue-600" />
                    Verfügbare Downloads
                  </h3>
                  <div className="grid grid-cols-1 gap-3">
                    {release.assets.map((asset, index: number) => (
                      <motion.div
                        key={asset.id}
                        initial={{ opacity: 0, x: -20 }}
                        animate={{ opacity: 1, x: 0 }}
                        transition={{ delay: 0.2 + index * 0.1 }}
                      >
                        <div className="flex flex-col sm:flex-row items-start sm:items-center gap-4 p-4 bg-white rounded-lg border border-gray-200 hover:border-blue-300 hover:shadow-md transition-all group">
                          <div className="p-3 bg-blue-50 rounded-lg group-hover:bg-blue-100 transition-colors">
                            <Package className="w-6 h-6 text-blue-600" />
                          </div>
                          <div className="flex-1 min-w-0">
                            <p className="font-semibold text-gray-900 truncate">{asset.name}</p>
                            <div className="flex items-center gap-3 mt-1">
                              <span className="text-sm text-gray-600">
                                {formatFileSize(asset.size)}
                              </span>
                              <span className="text-sm text-gray-600">
                                {asset.download_count} Downloads
                              </span>
                            </div>
                          </div>
                          <Button
                            onClick={() => handleDownload(asset)}
                            className="bg-blue-600 hover:bg-blue-700 w-full sm:w-auto"
                          >
                            <Download className="w-4 h-4 mr-2" />
                            Herunterladen
                          </Button>
                        </div>
                      </motion.div>
                    ))}
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>

          {/* System Requirements */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.3 }}
          >
            <Card>
              <CardHeader>
                <CardTitle>Systemanforderungen</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div>
                    <h3 className="font-semibold text-gray-900 mb-3">Mindestanforderungen</h3>
                    <ul className="space-y-2 text-sm text-gray-700">
                      <li className="flex items-center gap-2">
                        <CheckCircle className="w-4 h-4 text-green-600 flex-shrink-0" />
                        Windows 10 oder höher
                      </li>
                      <li className="flex items-center gap-2">
                        <CheckCircle className="w-4 h-4 text-green-600 flex-shrink-0" />
                        4 GB RAM
                      </li>
                      <li className="flex items-center gap-2">
                        <CheckCircle className="w-4 h-4 text-green-600 flex-shrink-0" />
                        500 MB freier Festplattenspeicher
                      </li>
                      <li className="flex items-center gap-2">
                        <CheckCircle className="w-4 h-4 text-green-600 flex-shrink-0" />
                        Internetverbindung für Updates
                      </li>
                    </ul>
                  </div>
                  <div>
                    <h3 className="font-semibold text-gray-900 mb-3">Empfohlen</h3>
                    <ul className="space-y-2 text-sm text-gray-700">
                      <li className="flex items-center gap-2">
                        <CheckCircle className="w-4 h-4 text-blue-600 flex-shrink-0" />
                        Windows 11
                      </li>
                      <li className="flex items-center gap-2">
                        <CheckCircle className="w-4 h-4 text-blue-600 flex-shrink-0" />
                        8 GB RAM oder mehr
                      </li>
                      <li className="flex items-center gap-2">
                        <CheckCircle className="w-4 h-4 text-blue-600 flex-shrink-0" />
                        SSD Festplatte
                      </li>
                      <li className="flex items-center gap-2">
                        <CheckCircle className="w-4 h-4 text-blue-600 flex-shrink-0" />
                        Permanente Internetverbindung
                      </li>
                    </ul>
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>

          {/* Installation Guide */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.4 }}
          >
            <Card>
              <CardHeader>
                <CardTitle>Installationsanleitung</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="space-y-4">
                  <div className="flex gap-4">
                    <div className="flex-shrink-0 w-8 h-8 bg-blue-600 text-white rounded-full flex items-center justify-center font-bold">
                      1
                    </div>
                    <div className="flex-1">
                      <h4 className="font-semibold text-gray-900 mb-1">Download starten</h4>
                      <p className="text-sm text-gray-600">
                        Laden Sie die aktuelle Version von PC-Wächter herunter.
                      </p>
                    </div>
                  </div>
                  <div className="flex gap-4">
                    <div className="flex-shrink-0 w-8 h-8 bg-blue-600 text-white rounded-full flex items-center justify-center font-bold">
                      2
                    </div>
                    <div className="flex-1">
                      <h4 className="font-semibold text-gray-900 mb-1">Installation ausführen</h4>
                      <p className="text-sm text-gray-600">
                        Führen Sie die heruntergeladene Datei mit Administratorrechten aus.
                      </p>
                    </div>
                  </div>
                  <div className="flex gap-4">
                    <div className="flex-shrink-0 w-8 h-8 bg-blue-600 text-white rounded-full flex items-center justify-center font-bold">
                      3
                    </div>
                    <div className="flex-1">
                      <h4 className="font-semibold text-gray-900 mb-1">Lizenz aktivieren</h4>
                      <p className="text-sm text-gray-600">
                        Geben Sie Ihren Lizenzschlüssel ein, um PC-Wächter zu aktivieren.
                      </p>
                    </div>
                  </div>
                  <div className="flex gap-4">
                    <div className="flex-shrink-0 w-8 h-8 bg-blue-600 text-white rounded-full flex items-center justify-center font-bold">
                      4
                    </div>
                    <div className="flex-1">
                      <h4 className="font-semibold text-gray-900 mb-1">Fertig!</h4>
                      <p className="text-sm text-gray-600">
                        PC-Wächter ist nun aktiv und schützt Ihr System.
                      </p>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          </motion.div>

          {/* Additional Resources */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.5 }}
          >
            <Card>
              <CardHeader>
                <CardTitle>Weitere Ressourcen</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <Button asChild variant="outline" className="h-auto p-4 flex flex-col items-start gap-2">
                    <Link to="/documentation">
                      <FileText className="w-6 h-6 text-blue-600" />
                      <div className="text-left">
                        <p className="font-semibold">Dokumentation</p>
                        <p className="text-xs text-gray-600">Vollständige Anleitung</p>
                      </div>
                    </Link>
                  </Button>
                  <Button
                    asChild
                    variant="outline"
                    className="h-auto p-4 flex flex-col items-start gap-2"
                  >
                    <a href={release.html_url || "https://github.com/cOOnStar/pcwaechter-public-release/releases"} target="_blank" rel="noopener noreferrer">
                      <History className="w-6 h-6 text-purple-600" />
                      <div className="text-left">
                        <p className="font-semibold">Changelog</p>
                        <p className="text-xs text-gray-600">Alle Änderungen</p>
                      </div>
                    </a>
                  </Button>
                  <Button
                    asChild
                    variant="outline"
                    className="h-auto p-4 flex flex-col items-start gap-2"
                  >
                    <a href="https://github.com/cOOnStar/pcwaechter-public-release" target="_blank" rel="noopener noreferrer">
                      <ExternalLink className="w-6 h-6 text-green-600" />
                      <div className="text-left">
                        <p className="font-semibold">GitHub Repository</p>
                        <p className="text-xs text-gray-600">Source Code ansehen</p>
                      </div>
                    </a>
                  </Button>
                </div>
              </CardContent>
            </Card>
          </motion.div>
        </>
      )}

      {/* Fallback when no release data */}
      {!loading && !error && !release && (
        <Card>
          <CardContent className="p-12">
            <div className="flex flex-col items-center justify-center text-center">
              <Package className="w-16 h-16 text-gray-400 mb-4" />
              <p className="text-gray-600 font-semibold mb-2">Keine Releases verfügbar</p>
              <p className="text-sm text-gray-500">Es wurden noch keine Releases veröffentlicht.</p>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
