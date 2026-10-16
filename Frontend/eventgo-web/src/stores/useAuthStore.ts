import { create } from "zustand";
import { User } from "../types";
import { setAccessToken } from "../services/api";

interface AuthState {
  user: User | null;
  token: string | null;
  isAuthenticated: boolean;
  login: (user: User, token: string) => void;
  logout: () => void;
}

const TOKEN_KEY = "eventgo_access_token";
const USER_KEY = "eventgo_user";

const getSavedToken = (): string | null => {
  return localStorage.getItem(TOKEN_KEY);
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
const initialUser = getSavedUser();

if (initialToken) {
  setAccessToken(initialToken);
}

export const useAuthStore = create<AuthState>((set) => ({
  user: initialUser,
  token: initialToken,
  isAuthenticated: Boolean(initialToken && initialUser),

  login: (user: User, token: string) => {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
    setAccessToken(token);
    set({ user, token, isAuthenticated: true });
  },

  logout: () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    setAccessToken(null);
    set({ user: null, token: null, isAuthenticated: false });
  },
}));
