import { Injectable } from '@angular/core';
import { Resolve, Router, ActivatedRouteSnapshot } from '@angular/router';
import { User } from '../_models/user';
import { UserService } from '../_services/user.service';
import { AlertifyService } from '../_services/alertify.service';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthenticationService } from '../_services/auth.service';

@Injectable()
export class MemberEditResolver implements Resolve<User> {
    constructor(
        private userService: UserService,
        private authService: AuthenticationService,
        private router: Router,
        private alertify: AlertifyService
    ) { }

    resolve(route: ActivatedRouteSnapshot): Observable<User> {
        return this.userService.getUser(this.authService.decodedToken.nameid).pipe(
            catchError(_ => {
                this.alertify.error('Problem retrieving data');
                this.router.navigate(['/members']);
                return of(null);
            })
        );
    }
}
