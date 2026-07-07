export interface User {
  id: string;
  email: string;
  notificationsEnabled: boolean;
  isAdmin: boolean;
}

export interface AuthResponse {
  accessToken: string;
  user: User;
}
