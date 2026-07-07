import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "./hooks/useAuth";
import { AuthPage } from "./pages/AuthPage";
import { ForgotPasswordPage } from "./pages/ForgotPasswordPage";
import { ResetPasswordPage } from "./pages/ResetPasswordPage";
import { PantryPage } from "./pages/PantryPage";
import { ScanPage } from "./pages/ScanPage";
import { ConfirmPage } from "./pages/ConfirmPage";
import { AddItemPage } from "./pages/AddItemPage";
import "./App.css";

function ProtectedRoute({ children }: { children: ReactNode }) {
  const { user, loading } = useAuth();
  if (loading) {
    return <p className="loading">Loading…</p>;
  }
  return user ? children : <Navigate to="/login" replace />;
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<AuthPage mode="login" />} />
        <Route path="/signup" element={<AuthPage mode="signup" />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route
          path="/pantry"
          element={
            <ProtectedRoute>
              <PantryPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/pantry/add"
          element={
            <ProtectedRoute>
              <AddItemPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/scan"
          element={
            <ProtectedRoute>
              <ScanPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/scan/confirm"
          element={
            <ProtectedRoute>
              <ConfirmPage />
            </ProtectedRoute>
          }
        />
        <Route path="*" element={<Navigate to="/pantry" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
