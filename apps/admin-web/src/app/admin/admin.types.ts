import type { UserRole } from '../saas/saas.types';

export interface AdminUserSummary {
  id: string;
  email: string;
  nome: string | null;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
  medicoId: string | null;
}

export interface ProfileSummary {
  id: string;
  email: string;
  nome: string | null;
  role: string;
}

export interface UpdateProfilePayload {
  nome: string;
}
