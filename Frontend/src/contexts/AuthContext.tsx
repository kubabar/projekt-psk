import React, { createContext, useContext, useState, useEffect } from 'react';
import { websocketService } from '../services/websocket';
import type { User } from '../types';

interface AuthContextType {
  user: User | null;
  login: (user: User) => Promise<void>;
  logout: () => void;
  isAuthenticated: boolean;
  isLoading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('token');
    const userId = localStorage.getItem('userId');
    const email = localStorage.getItem('email');

    if (token && userId && email) {
      const parsedUserId = parseInt(userId, 10);
      if (!isNaN(parsedUserId)) {
        setUser({ userId: parsedUserId, email, token });
        websocketService.connect(userId).catch(console.error);
      } else {
        // Invalid userId, clear localStorage
        localStorage.removeItem('token');
        localStorage.removeItem('userId');
        localStorage.removeItem('email');
      }
    }
    
    setIsLoading(false);
  }, []);

  const login = async (userData: User) => {
    setUser(userData);
    localStorage.setItem('token', userData.token);
    localStorage.setItem('userId', userData.userId.toString());
    localStorage.setItem('email', userData.email);
    
    await websocketService.connect(userData.userId.toString());
  };

  const logout = () => {
    setUser(null);
    localStorage.removeItem('token');
    localStorage.removeItem('userId');
    localStorage.removeItem('email');
    websocketService.disconnect();
  };

  return (
    <AuthContext.Provider value={{ user, login, logout, isAuthenticated: !!user, isLoading }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
};
