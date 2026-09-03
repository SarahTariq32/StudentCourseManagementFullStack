export interface RegisterRequest {
  fullName: string;
  email: string;
  username: string;
  password: string;
  role: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  refreshTokenExpiryTime: string;
}