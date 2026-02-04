export interface User {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  createdAt: string;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface LoginRequest {
  email: string;
  password: string;
  deviceId: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
  deviceId: string;
}

export interface RefreshRequest {
  refreshToken: string;
  deviceId: string;
}

export interface LogoutRequest {
  deviceId: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: User;
}
