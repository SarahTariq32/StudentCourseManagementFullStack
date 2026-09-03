import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login';
import { RegisterComponent } from './components/register/register';
import { StudentDashboardComponent } from './components/student-dashboard/student-dashboard';
import { AdminDashboardComponent } from './components/admin-dashboard/admin-dashboard';
import { roleGuard } from './guards/role-guard';

export const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  { 
    path: 'student-dashboard', 
    component: StudentDashboardComponent, 
    canActivate: [roleGuard('Student')] 
  },
  { 
    path: 'admin-dashboard', 
    component: AdminDashboardComponent, 
    canActivate: [roleGuard('Admin')] 
  },
  { path: '**', redirectTo: 'login' }
];