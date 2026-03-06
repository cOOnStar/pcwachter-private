import { motion } from "motion/react";
import { useMemo, useState } from "react";
import { Link } from "react-router";
import {
  BookOpen,
  ChevronRight,
  FileQuestion,
  FileText,
  HelpCircle,
  Lightbulb,
  Search,
  Settings,
  Video,
  Wrench,
  Zap,
} from "lucide-react";

import { PREVIEW_BOOTSTRAP, usePortalBootstrap } from "../../hooks";
import { Card, CardContent, CardHeader, CardTitle } from "../components/ui/card";
import { Input } from "../components/ui/input";

const iconMap = {
  BookOpen,
  Video,
  FileQuestion,
  Lightbulb,
  Wrench,
  Settings,
  Zap,
} as const;

export function Documentation() {
  const { data } = usePortalBootstrap();
  const portal = data ?? PREVIEW_BOOTSTRAP;
  const [searchTerm, setSearchTerm] = useState("");

  const filteredCategories = useMemo(
    () =>
      portal.documentationCategories
        .map((category) => ({
          ...category,
          articles: category.articles.filter(
            (article) =>
              article.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
              article.views.toLowerCase().includes(searchTerm.toLowerCase()),
          ),
        }))
        .filter((category) => category.articles.length > 0),
    [portal.documentationCategories, searchTerm],
  );

  return (
    <div className="p-4 md:p-6 space-y-6 max-w-7xl mx-auto">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="text-center space-y-4">
        <div className="inline-flex items-center justify-center w-16 h-16 bg-gradient-to-br from-blue-50 to-purple-50 rounded-2xl mb-4">
          <BookOpen className="w-8 h-8 text-blue-600" />
        </div>
        <h1 className="text-3xl md:text-4xl font-bold text-gray-900">Dokumentation</h1>
        <p className="text-lg text-gray-600 max-w-2xl mx-auto">Alles, was Sie über PC-Wächter wissen müssen</p>
      </motion.div>

      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="max-w-2xl mx-auto">
        <Card>
          <CardContent className="p-4">
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
              <Input placeholder="Dokumentation durchsuchen..." value={searchTerm} onChange={(event) => setSearchTerm(event.target.value)} className="pl-10 h-12 text-lg" />
            </div>
          </CardContent>
        </Card>
      </motion.div>

      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.2 }}>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {portal.documentation.slice(0, 3).map((document, index) => {
            const icons = [FileText, Settings, Wrench];
            const Icon = icons[index] ?? FileText;
            const colors = ["border-blue-100 hover:border-blue-300", "border-green-100 hover:border-green-300", "border-purple-100 hover:border-purple-300"];
            return (
              <Card key={document.id} className={`hover:shadow-lg transition-shadow cursor-pointer border-2 ${colors[index] ?? colors[0]} h-full`}>
                <CardContent className="p-4">
                  <div className="flex items-center gap-3">
                    <div className="p-2 bg-gray-50 rounded-lg">
                      <Icon className="w-5 h-5 text-blue-600" />
                    </div>
                    <div>
                      <h3 className="font-semibold text-gray-900 text-sm">{document.name}</h3>
                      <p className="text-xs text-gray-600">{document.version} · {document.size}</p>
                    </div>
                  </div>
                </CardContent>
              </Card>
            );
          })}
        </div>
      </motion.div>

      <div className="space-y-6">
        {filteredCategories.length > 0 ? (
          filteredCategories.map((category, index) => {
            const Icon = iconMap[category.icon as keyof typeof iconMap] ?? BookOpen;
            return (
              <motion.div key={category.title} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.3 + index * 0.1 }}>
                <Card>
                  <CardHeader>
                    <div className="flex items-center gap-4">
                      <div className={`p-3 rounded-lg ${category.bgColor}`}>
                        <Icon className={`w-6 h-6 ${category.color}`} />
                      </div>
                      <div>
                        <CardTitle className="text-xl">{category.title}</CardTitle>
                        <p className="text-sm text-gray-600 mt-1">Wissensbereich mit Artikeln und Hilfestellungen</p>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent>
                    <div className="space-y-2">
                      {category.articles.map((article) => (
                        <div key={`${category.title}-${article.title}`} className="flex items-center justify-between p-4 rounded-lg border border-gray-200 hover:border-blue-300 hover:bg-blue-50 transition-all group">
                          <div className="flex-1">
                            <p className="font-semibold text-gray-900 group-hover:text-blue-700 transition-colors">{article.title}</p>
                            <p className="text-sm text-gray-600 mt-1">{article.views} Aufrufe</p>
                          </div>
                          <ChevronRight className="w-5 h-5 text-gray-400 group-hover:text-blue-600 transition-colors ml-4" />
                        </div>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              </motion.div>
            );
          })
        ) : (
          <Card>
            <CardContent className="p-12">
              <div className="flex flex-col items-center justify-center text-center">
                <Search className="w-16 h-16 text-gray-400 mb-4" />
                <p className="text-gray-600 font-semibold mb-2">Keine Ergebnisse gefunden</p>
                <p className="text-sm text-gray-500">Versuchen Sie andere Suchbegriffe.</p>
              </div>
            </CardContent>
          </Card>
        )}
      </div>

      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.6 }}>
        <Card>
          <CardHeader>
            <CardTitle>Beliebte Artikel</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {portal.popularArticles.map((article) => (
              <div key={`${article.category}-${article.title}`} className="flex items-center justify-between rounded-lg bg-gray-50 px-4 py-3">
                <div>
                  <p className="font-medium text-gray-900">{article.title}</p>
                  <p className="text-sm text-gray-600">{article.category} · {article.views}</p>
                </div>
                <div className="text-sm text-amber-600 font-semibold">{article.rating.toFixed(1)}/5</div>
              </div>
            ))}
          </CardContent>
        </Card>
      </motion.div>

      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.7 }}>
        <Card className="bg-gradient-to-br from-blue-50 to-purple-50 border-2 border-blue-200">
          <CardContent className="p-8">
            <div className="flex flex-col md:flex-row items-center justify-between gap-6">
              <div className="flex items-center gap-4">
                <div className="p-4 bg-white rounded-xl shadow-sm">
                  <HelpCircle className="w-8 h-8 text-blue-600" />
                </div>
                <div>
                  <h3 className="text-xl font-bold text-gray-900 mb-1">Brauchen Sie weitere Hilfe?</h3>
                  <p className="text-gray-600">Unser Support-Team steht Ihnen gerne zur Verfügung.</p>
                </div>
              </div>
              <Link to="/support" className="px-6 py-3 bg-white border-2 border-blue-600 text-blue-600 rounded-lg font-semibold hover:bg-blue-50 transition-colors">
                Support kontaktieren
              </Link>
            </div>
          </CardContent>
        </Card>
      </motion.div>
    </div>
  );
}
