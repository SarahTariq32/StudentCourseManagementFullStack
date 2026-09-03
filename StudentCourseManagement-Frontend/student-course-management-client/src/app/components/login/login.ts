import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class LoginComponent {
  loginForm: FormGroup;
  errorMessage: string = '';
  isLoading: boolean = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.loginForm = this.fb.group({
      username: ['', [Validators.required]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  get f() {
    return this.loginForm.controls;
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.login(this.loginForm.value).subscribe({
      next: (res) => {
        try {
          localStorage.setItem('token', res.token);
          if (res.refreshToken) {
            localStorage.setItem('refreshToken', res.refreshToken);
          }

          // Safely extract role from JWT payload
          const payloadBase64 = res.token.split('.')[1];
          const payload = JSON.parse(atob(payloadBase64));

          // .NET JWT Claim Keys mapping
          const role = payload['role'] || 
                       payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 
                       '';

          this.isLoading = false;

          if (role.toString().toLowerCase() === 'admin') {
            this.router.navigate(['/admin-dashboard']);
          } else {
            this.router.navigate(['/student-dashboard']);
          }
        } catch (e) {
          console.error('Error decoding token:', e);
          this.isLoading = false;
          // Fallback route if role parsing fails
          this.router.navigate(['/student-dashboard']);
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err.error?.message || err.error || 'Invalid username or password.';
      }
    });
  }
}