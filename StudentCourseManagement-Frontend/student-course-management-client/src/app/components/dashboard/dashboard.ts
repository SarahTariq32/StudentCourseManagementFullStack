import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { CourseService } from '../../services/course';
import { Course } from '../../models/course.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class DashboardComponent implements OnInit {
  courses: Course[] = [];
  isLoading: boolean = true;
  errorMessage: string = '';

  constructor(
    private courseService: CourseService,
    private router: Router,
    private cdr: ChangeDetectorRef // <-- INJECT CHANGE DETECTOR
  ) {}

  ngOnInit(): void {
    this.loadCourses();
  }

  loadCourses(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.courseService.getCourses().subscribe({
      next: (data: Course[]) => {
        this.courses = data;
        this.isLoading = false;
        this.cdr.detectChanges(); // <-- FORCE ANGULAR TO UPDATE THE UI NOW
      },
      error: (err: any) => {
        console.error('API Error:', err);
        this.errorMessage = 'Failed to load course list.';
        this.isLoading = false;
        this.cdr.detectChanges(); // <-- FORCE ANGULAR TO UPDATE ON ERROR TOO
      }
    });
  }

  onLogout(): void {
    localStorage.clear();
    this.router.navigate(['/login']);
  }
}