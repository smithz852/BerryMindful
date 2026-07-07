export interface AdminUser {
  id: string;
  email: string;
  createdAt: string;
  isAdmin: boolean;
  pantryItemCount: number;
  receiptCount: number;
}

export interface AdminStats {
  totalUsers: number;
  newUsersThisWeek: number;
  totalReceipts: number;
  totalPantryItems: number;
}

export interface WeeklySignups {
  weekStart: string;
  count: number;
}
