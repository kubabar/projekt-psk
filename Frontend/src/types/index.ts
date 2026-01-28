export interface User {
  userId: number;
  email: string;
  token: string;
}

export interface ApiResponse<T = any> {
  success: boolean;
  message?: string;
  data?: T;
  error?: string;
}

export interface LoginResponse {
  sessionId: string;
  userId: number;
  passwordExpired: boolean;
}

export interface VerifyCodeResponse {
  token: string;
  userId: number;
}

export interface TaskAcceptedResponse {
  taskId: string;
  message: string;
}

export interface ApiTaskResponse {
  taskId: string;
  success: boolean;
  data?: any;
  error?: string;
  completedAt: string;
}
