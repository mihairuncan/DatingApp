import { Injectable } from '@angular/core';
import { CanDeactivate } from '@angular/router';
import { MemberEditComponent } from '../members/member-edit/member-edit.component';

@Injectable()
export class PreventUnsavedChanges implements CanDeactivate<MemberEditComponent> {
    canDeactivate(component: MemberEditComponent) {
        if (component.editForm.value.gender && component.editForm.value.dateOfBirth && component.editForm.submitted) {

            if (component.editForm.dirty) {
                return confirm('Are you sure you want to continue? Any unsaved changes will be lost');
            }
            return true;
        }
        if (!localStorage.getItem('token')) {
            return true;
        }
        return false;
    }
}
