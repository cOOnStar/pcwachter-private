import { useState, useEffect } from 'react';
import type { GitHubRelease } from '../types';
import { fetchLatestRelease } from '../lib/api';
import { IS_PREVIEW } from '../lib/keycloak';

interface UseGitHubReleaseResult {
  release: GitHubRelease | null;
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

const CACHE_KEY = 'github_release_cache';
const CACHE_DURATION = 5 * 60 * 1000; // 5 minutes

interface CachedData {
  data: GitHubRelease;
  timestamp: number;
}

export function useGitHubRelease(): UseGitHubReleaseResult {
  const [release, setRelease] = useState<GitHubRelease | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchRelease = async () => {
    try {
      if (IS_PREVIEW) {
        setRelease({
          tag_name: 'v2.6.0',
          name: 'PC-Wächter 2.6.0',
          body: 'Preview-Modus',
          published_at: '2026-03-04T10:00:00Z',
          html_url: 'https://github.com/cOOnStar/pcwaechter-public-release/releases/latest',
          assets: [],
        });
        setError(null);
        setLoading(false);
        return;
      }

      // Check cache first
      const cached = localStorage.getItem(CACHE_KEY);
      if (cached) {
        const { data, timestamp }: CachedData = JSON.parse(cached);
        if (Date.now() - timestamp < CACHE_DURATION) {
          setRelease(data);
          setLoading(false);
          return;
        }
      }

      const data = await fetchLatestRelease();
      if (!data) {
        setRelease(null);
        setError(null);
        setLoading(false);
        return;
      }

      // Cache the result
      localStorage.setItem(
        CACHE_KEY,
        JSON.stringify({
          data,
          timestamp: Date.now(),
        })
      );

      setRelease(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ein Fehler ist aufgetreten');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchRelease();
  }, []);

  const refetch = () => {
    localStorage.removeItem(CACHE_KEY);
    setLoading(true);
    fetchRelease();
  };

  return { release, loading, error, refetch };
}
