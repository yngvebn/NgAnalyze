import { Action } from '@ngrx/store';import { otherAction, OtherAction } from './actions'

export class Service {

    public action: OtherAction =otherAction(99, null, 'Hello world');

    public doSomething() {
        this.dispatch(otherAction(44));
    }

    dispatch(action: Action) {

    }
}
