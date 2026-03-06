import { Outlet } from 'react-router';
import { ProtectedRoute } from '../auth/ProtectedRoute';

export function ProtectedLayout() {
  return (
    <ProtectedRoute>
      <Outlet />
    </ProtectedRoute>
  );
}
