import { useEffect, useState } from 'react';
import { Progress } from './progress';
import { motion, AnimatePresence } from 'motion/react';

let globalLoadingState = {
  loading: false,
  listeners: new Set<(loading: boolean) => void>(),
};

export function showGlobalLoading() {
  globalLoadingState.loading = true;
  globalLoadingState.listeners.forEach((listener) => listener(true));
}

export function hideGlobalLoading() {
  globalLoadingState.loading = false;
  globalLoadingState.listeners.forEach((listener) => listener(false));
}

export function GlobalLoadingIndicator() {
  const [isLoading, setIsLoading] = useState(false);
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    const listener = (loading: boolean) => {
      setIsLoading(loading);
      if (loading) {
        setProgress(0);
        // Simulate progress
        const interval = setInterval(() => {
          setProgress((prev) => {
            if (prev >= 90) {
              clearInterval(interval);
              return 90;
            }
            return prev + 10;
          });
        }, 200);
        return () => clearInterval(interval);
      } else {
        setProgress(100);
        setTimeout(() => setProgress(0), 500);
      }
    };

    globalLoadingState.listeners.add(listener);
    return () => {
      globalLoadingState.listeners.delete(listener);
    };
  }, []);

  return (
    <AnimatePresence>
      {isLoading && (
        <motion.div
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="fixed top-0 left-0 right-0 z-[100]"
        >
          <Progress value={progress} className="h-1 rounded-none" />
        </motion.div>
      )}
    </AnimatePresence>
  );
}