export interface User {
  id: string;
  email: string;
  notificationsEnabled: boolean;
}

export interface AuthResponse {
  accessToken: string;
  user: User;
}
