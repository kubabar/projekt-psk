import axios from 'axios';
import type { ApiResponse, LoginResponse, VerifyCodeResponse, TaskAcceptedResponse } from '../types';

//const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:8080';

export const api = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json'
  }
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export const authAPI = {
  register: (email: string, password: string) =>
    api.post<ApiResponse>('/auth/register', { email, password }),

  login: (email: string, password: string) =>
    api.post<ApiResponse<LoginResponse>>('/auth/login', {
      email,
      password,
      ipAddress: '127.0.0.1',
      userAgent: navigator.userAgent
    }),

  sendCode: (sessionId: string, email: string) =>
    api.post<ApiResponse>('/auth/send-code', { sessionId, email }),

  verifyCode: (sessionId: string, code: string) =>
    api.post<ApiResponse<VerifyCodeResponse>>('/auth/verify-code', { sessionId, code }),

  changePassword: (oldPassword: string, newPassword: string) =>
    api.post<ApiResponse>('/auth/change-password', { oldPassword, newPassword }),

  requestPasswordReset: (email: string) =>
    api.post<ApiResponse>('/auth/request-password-reset', { email, ipAddress: '127.0.0.1' }),

  resetPassword: (email: string, token: string, newPassword: string) =>
    api.post<ApiResponse>('/auth/reset-password', { email, token, newPassword })
};

export const externalAPI = {
  getCompanyInfo: (nip: string) =>
    api.post<ApiResponse<TaskAcceptedResponse>>('/api/company-info', { nip }),

  getCurrencyRate: (currencyCode: string) =>
    api.post<ApiResponse<TaskAcceptedResponse>>('/api/currency-rate', { currencyCode })
};
