import { Action } from '@ngrx/store';

/**
 * @deprecated Use other method
 */
export enum ActionTypes {
    TestAction = 'TestAction'
}

export const OTHER_ACTION = 'Other action';

export class TestAction implements Action {
    type = ActionTypes.TestAction;
    constructor(public name: string) { }
}/* New value goes here */

export class ThirdAction implements Action {
    type = 'Third Action';

    constructor(public isValid: boolean) { }
}