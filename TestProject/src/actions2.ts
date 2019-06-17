import { Action } from '@ngrx/store';

export class NewAction implements Action {
    type = 'New action';

    constructor(public name: string) { }
}

export type AllActions = NewAction;