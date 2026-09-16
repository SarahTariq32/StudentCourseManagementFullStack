import { TestBed } from '@angular/core/testing';

import { AiCourse } from './ai-course';

describe('AiCourse', () => {
  let service: AiCourse;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(AiCourse);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });
});
