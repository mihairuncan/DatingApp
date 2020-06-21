import { Component, OnInit, ViewChild, HostListener } from '@angular/core';
import { User } from 'src/app/_models/user';
import { ActivatedRoute } from '@angular/router';
import { AlertifyService } from 'src/app/_services/alertify.service';
import { NgForm } from '@angular/forms';
import { UserService } from 'src/app/_services/user.service';
import { AuthenticationService } from 'src/app/_services/auth.service';
import { BsDatepickerConfig } from 'ngx-bootstrap/datepicker/public_api';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-member-edit',
  templateUrl: './member-edit.component.html',
  styleUrls: ['./member-edit.component.css']
})
export class MemberEditComponent implements OnInit {
  userDataIsValid = true;
  bsConfig: Partial<BsDatepickerConfig>;
  @ViewChild('editForm', { static: true }) editForm: NgForm;
  user: User;
  photoUrl: string;
  @HostListener('window:beforeunload', ['$event'])
  unloadNotification($event: any) {
    if (this.editForm.dirty) {
      $event.returnValue = true;
    }
  }

  constructor(
    private route: ActivatedRoute,
    private alertify: AlertifyService,
    private userService: UserService,
    private authService: AuthenticationService,
    private datePipe: DatePipe
  ) { }

  ngOnInit() {
    this.bsConfig = {
      containerClass: 'theme-red',
      dateInputFormat: 'YYYY-MM-DD'
    };
    this.route.data.subscribe(data => {
      this.user = data['user'];
      if (this.user.dateOfBirth === '0001-01-01T00:00:00' || !this.user.gender) {
        this.user.dateOfBirth = null;
        this.userDataIsValid = false;
      }
    });
    this.authService.currentPhotoUrl.subscribe(photoUrl => this.photoUrl = photoUrl);
  }

  updateUser() {
    if (!this.user.dateOfBirth) {
      this.alertify.error('Invalid date of birth');
    } else {
      this.user.dateOfBirth = this.datePipe.transform(this.user.dateOfBirth, 'yyyy-MM-dd');
      this.userService.updateUser(this.authService.decodedToken.nameid, this.user).subscribe(next => {
        this.alertify.success('Profile updated successfully');
        this.editForm.reset(this.user);
        if (this.user.dateOfBirth !== '0001-01-01T00:00:00' && this.user.gender) {
          this.userDataIsValid = true;
        }
      }, error => {
        this.alertify.error(error);
      });
    }
  }

  updateMainPhoto(photoUrl) {
    this.user.photoUrl = photoUrl;
  }
}
