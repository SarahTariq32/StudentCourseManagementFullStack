import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminService, PendingRequest } from '../../services/admin';

type AdminView = 
  | 'overview' 
  | 'students-list' 
  | 'student-search' 
  | 'student-edit' 
  | 'courses-list' 
  | 'course-search'
  | 'course-form' 
  | 'registration-requests'
  | 'course-requests';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './admin-dashboard.html',
  styleUrl: './admin-dashboard.scss'
})
export class AdminDashboardComponent implements OnInit {
  activeView: AdminView = 'overview';
  
  students: any[] = [];
  courses: any[] = [];
  
  registrationRequests: PendingRequest[] = [];
  courseRequests: PendingRequest[] = [];
  
  searchedStudent: any = null;
  searchedCourse: any = null;
  selectedStudentId: number | null = null;
  selectedCourseId: number | null = null;

  studentEditForm: FormGroup;
  courseForm: FormGroup;
  
  statusMessage: string = '';
  errorMessage: string = '';

  constructor(
    private adminService: AdminService,
    private fb: FormBuilder,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {
    this.studentEditForm = this.fb.group({
      name: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      age: [20, [Validators.required, Validators.min(16)]]
    });

    this.courseForm = this.fb.group({
      name: ['', Validators.required],
      credits: [3, [Validators.required, Validators.min(1), Validators.max(6)]]
    });
  }

  ngOnInit(): void {
    this.refreshAllData();
  }

  setView(view: AdminView): void {
    this.activeView = view;
    this.statusMessage = '';
    this.errorMessage = '';
    this.searchedStudent = null;
    this.searchedCourse = null;
  }

  refreshAllData(): void {
    // 1. Fetch Students (Handles both direct array or paged wrapper payload, sorted by ID ascending)
    this.adminService.getStudents().subscribe({
      next: (res: any) => { 
        const raw = Array.isArray(res) ? res : (res?.items || []); 
        this.students = raw.slice().sort((a: any, b: any) => (a.id || 0) - (b.id || 0));
        this.cdr.detectChanges(); 
      },
      error: (err) => {
        console.error('Students fetch error:', err);
        this.errorMessage = 'Failed to load student directory.';
      }
    });

    // 2. Fetch Courses (Handles array or wrapped payload, sorted by ID ascending)
    this.adminService.getCourses().subscribe({
      next: (res: any) => { 
        const raw = Array.isArray(res) ? res : (res?.items || []); 
        this.courses = raw.slice().sort((a: any, b: any) => (a.id || 0) - (b.id || 0));
        this.cdr.detectChanges(); 
      },
      error: (err) => {
        console.error('Courses fetch error:', err);
        this.errorMessage = 'Failed to load course catalog.';
      }
    });

    // 3. Fetch Pending Requests
    this.adminService.getPendingRequests().subscribe({
      next: (requests: PendingRequest[]) => {
        const allReqs = requests || [];

        this.registrationRequests = allReqs.filter(r => 
          r.requestType?.toLowerCase().includes('register') || 
          r.reason?.includes('ACCOUNT_CREATION_REQUEST')
        );

        this.courseRequests = allReqs.filter(r => 
          r.requestType?.toLowerCase().includes('enroll') || 
          r.requestType?.toLowerCase().includes('unenroll')
        );

        this.cdr.detectChanges();
      },
      error: (err) => console.error('Requests fetch error:', err)
    });
  }

  // --- COURSE ACTIONS ---
  onSearchCourse(idInput: string): void {
    const id = parseInt(idInput, 10);
    if (!id) return;

    this.adminService.getCourseById(id).subscribe({
      next: (res) => {
        this.searchedCourse = res;
        this.errorMessage = '';
        this.cdr.detectChanges();
      },
      error: () => {
        this.searchedCourse = null;
        this.errorMessage = `Course ID #${id} not found.`;
        this.cdr.detectChanges();
      }
    });
  }

  openEditCourse(course: any): void {
    this.selectedCourseId = course.id;
    this.courseForm.patchValue({ name: course.name, credits: course.credits });
    this.setView('course-form');
  }

  onSaveCourse(): void {
    if (this.courseForm.invalid) return;

    if (this.selectedCourseId) {
      this.adminService.updateCourse(this.selectedCourseId, this.courseForm.value).subscribe({
        next: () => {
          this.statusMessage = 'Course updated successfully!';
          this.refreshAllData();
          this.setView('courses-list');
        },
        error: (err) => this.errorMessage = err.error?.message || 'Update failed.'
      });
    } else {
      this.adminService.createCourse(this.courseForm.value).subscribe({
        next: () => {
          this.statusMessage = 'New course created successfully!';
          this.refreshAllData();
          this.setView('courses-list');
        },
        error: (err) => this.errorMessage = err.error?.message || 'Creation failed.'
      });
    }
  }

  onDeleteCourse(id: number): void {
    if (!confirm(`Are you sure you want to delete Course ID #${id}?`)) return;

    this.adminService.deleteCourse(id).subscribe({
      next: () => {
        this.statusMessage = `Course #${id} deleted successfully.`;
        this.searchedCourse = null;
        this.refreshAllData();
      },
      error: (err) => this.errorMessage = err.error?.message || 'Delete operation failed.'
    });
  }

  // --- STUDENT ACTIONS ---
  onSearchStudent(idInput: string): void {
    const id = parseInt(idInput, 10);
    if (!id) return;

    this.adminService.getStudentById(id).subscribe({
      next: (res) => {
        this.searchedStudent = res;
        this.errorMessage = '';
        this.cdr.detectChanges();
      },
      error: () => {
        this.searchedStudent = null;
        this.errorMessage = `Student ID #${id} not found.`;
        this.cdr.detectChanges();
      }
    });
  }

  openEditStudent(student: any): void {
    this.selectedStudentId = student.id;
    this.studentEditForm.patchValue({ name: student.name, email: student.email, age: student.age });
    this.setView('student-edit');
  }

  onUpdateStudent(): void {
    if (this.studentEditForm.invalid || !this.selectedStudentId) return;

    this.adminService.updateStudent(this.selectedStudentId, this.studentEditForm.value).subscribe({
      next: () => {
        this.statusMessage = 'Student profile updated successfully!';
        this.refreshAllData();
        this.setView('students-list');
      },
      error: (err) => this.errorMessage = err.error?.message || 'Update failed.'
    });
  }

  onDeleteStudent(id: number): void {
    if (!confirm(`Are you sure you want to delete Student ID #${id}?`)) return;

    this.adminService.deleteStudent(id).subscribe({
      next: () => {
        this.statusMessage = 'Student profile deleted successfully.';
        this.searchedStudent = null;
        this.refreshAllData();
      },
      error: (err) => this.errorMessage = err.error?.message || 'Delete operation failed.'
    });
  }

  // --- PROCESS REQUESTS ---
  onProcessRequest(requestId: number, approve: boolean): void {
    this.adminService.processRequest(requestId, approve).subscribe({
      next: (res) => {
        this.statusMessage = res.message || (approve ? 'Approved successfully.' : 'Rejected.');
        this.refreshAllData();
      },
      error: (err) => this.errorMessage = err.error?.message || 'Processing failed.'
    });
  }

  cleanReason(reason: string): string {
    if (!reason) return 'Requested account verification.';
    return reason.replace(/^ACCOUNT_CREATION_REQUEST:\s*/i, '').trim() || 'Requested account verification.';
  }
  onLogout(): void {
    localStorage.clear();
    this.router.navigate(['/login']);
  }
}