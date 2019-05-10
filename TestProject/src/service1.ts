import { Action } from '@ngrx/store';
import * as actions from './actions';

export class Service {

    public action: actions.OtherAction =actions.otherAction(99, null, 'Hello world');

    public doSomething() {
        this.dispatch(actions.otherAction(44));

        this.dispatch(actions.otherAction(58));

        this.dispatch(actions.testAction('My name'));
    }

    dispatch(action: Action) {

    }
}
