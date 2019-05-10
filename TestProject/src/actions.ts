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


export const otherAction = createAction(
	OTHER_ACTION,
	(age: number, min?: number, meta: string = 'test', stuff: any = null, data: number = 10) => ({ age, min, meta, stuff, data })
);


export const thirdAction = createAction(
	'Third Action',
	(isValid: boolean) => ({ isValid })
);



export type AllActions = TestAction |
    OtherAction |
    ThirdAction;