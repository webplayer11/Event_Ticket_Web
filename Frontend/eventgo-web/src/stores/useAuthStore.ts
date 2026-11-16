import { create } from "zustand";
import { User } from "../types";
import {
  setAccessToken,
  setRefreshToken,
  setAuthListeners,
} from "../services/api";

interface AuthState {
  user: User | null;
  token: string | null;
  refreshToken: string | null;
  isAuthenticated: boolean;
  login: (user: User, token: string, refreshToken?: string) => void;
  setTokens: (token: string, refreshToken?: string) => void;
  logout: () => void;
}

const TOKEN_KEY = "eventgo_access_token";
const REFRESH_TOKEN_KEY = "eventgo_refresh_token";
const USER_KEY = "eventgo_user";

const getSavedToken = (): string | null => {
  return localStorage.getItem(TOKEN_KEY);
};

const getSavedRefreshToken = (): string | null => {
  return localStorage.getItem(REFRESH_TOKEN_KEY);
};

const getSavedUser = (): User | null => {
  const data = localStorage.getItem(USER_KEY);
  if (!data) return null;
  try {
    return JSON.parse(data) as User;
  } catch {
    return null;
  }
};

const initialToken = getSavedToken();
const initialRefreshToken = getSavedRefreshToken();
const initialUser = getSavedUser();

if (initialToken) {
  setAccessToken(initialToken);
}
if (initialRefreshToken) {
  setRefreshToken(initialRefreshToken);
}

export const useAuthStore = create<AuthState>((set) => {
  // Listen for automatic token refresh or auth failure events from api.ts
  setAuthListeners({
    onTokenRefresh: (newAccessToken, newRefreshToken) => {
      localStorage.setItem(TOKEN_KEY, newAccessToken);
      localStorage.setItem(REFRESH_TOKEN_KEY, newRefreshToken);
      set({ token: newAccessToken, refreshToken: newRefreshToken });
    },
    onAuthFailure: () => {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(REFRESH_TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
      set({ user: null, token: null, refreshToken: null, isAuthenticated: false });
    },
  });

  return {
    user: initialUser,
    token: initialToken,
    refreshToken: initialRefreshToken,
    isAuthenticated: Boolean(initialToken && initialUser),

    login: (user: User, token: string, refreshToken?: string) => {
      localStorage.setItem(TOKEN_KEY, token);
      setAccessToken(token);

      if (refreshToken) {
        localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
        setRefreshToken(refreshToken);
      } else {
        localStorage.removeItem(REFRESH_TOKEN_KEY);
        setRefreshToken(null);
      }

      localStorage.setItem(USER_KEY, JSON.stringify(user));
      set({
        user,
        token,
        refreshToken: refreshToken ?? null,
        isAuthenticated: true,
      });
    },

    setTokens: (token: string, refreshToken?: string) => {
      localStorage.setItem(TOKEN_KEY, token);
      setAccessToken(token);

      if (refreshToken) {
        localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
        setRefreshToken(refreshToken);
        set({ token, refreshToken });
      } else {
        set({ token });
      }
    },

    logout: () => {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(REFRESH_TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
      setAccessToken(null);
      setRefreshToken(null);
      set({
        user: null,
        token: null,
        refreshToken: null,
        isAuthenticated: false,
      });
    },
  };
});
