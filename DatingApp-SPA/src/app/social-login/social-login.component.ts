import { Component, OnInit } from '@angular/core';
import { AuthService } from 'angularx-social-login';
import { SocialUser } from 'angularx-social-login';
import { GoogleLoginProvider } from 'angularx-social-login';
import { AuthenticationService } from '../_services/auth.service';
import { AlertifyService } from '../_services/alertify.service';
import { Router } from '@angular/router';
import { User } from '../_models/user';

@Component({
  selector: 'app-social-login',
  templateUrl: './social-login.component.html',
  styleUrls: ['./social-login.component.css']
})
export class SocialLoginComponent implements OnInit {
  user: SocialUser;
  private loggedIn: boolean;

  constructor(
    private socialAuthService: AuthService,
    private authService: AuthenticationService,
    private alertify: AlertifyService,
    private router: Router
  ) { }

  ngOnInit() {
    this.socialAuthService.authState.subscribe((user) => {
      this.user = user;
      this.loggedIn = (user != null);
    });
  }


  signInWithGoogle(): void {
    this.socialAuthService.signIn(GoogleLoginProvider.PROVIDER_ID).finally(() => {
      this.authService.loginGoogle(this.user.idToken).subscribe(_ => {
        this.alertify.success('Logged in successfully');
      }, error => {
        this.alertify.error(error);
      }, () => {
        const user: User = JSON.parse(localStorage.getItem('user'));
        if (!user.gender) {
          this.router.navigate(['member/edit']);
        } else {
          this.router.navigate(['members']);

        }
      });
    });
  }

  signOut(): void {
    this.socialAuthService.signOut();
  }

}

