export interface RegisterRequest {
  username: string;
  password: string;
  role: string; // 'Student' or 'Admin'
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  refreshTokenExpiryTime: string;
}