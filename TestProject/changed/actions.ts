import { Action, createAction, props } from '@ngrx/store'

/**
 * @deprecated Use other method
 */
export enum ActionTypes {
    TestAction = 'TestAction'
}

export const OTHER_ACTION = 'Other action';
export const testAction = createAction(
	ActionTypes.TestAction,
	(name: string) => ({ name })
);



export class OtherAction implements Action {
    type = OTHER_ACTION;

    constructor(public age: number, public min?: number, public meta: string = 'test', public stuff: any = null, public data: number = 10) { }
}

export class ThirdAction implements Action {
    type = 'Third Action';

    constructor(public isValid: boolean) { }
}