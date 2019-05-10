import { Action } from '@ngrx/store';
import * as actions from './actions';

export class Service {

    public action: actions.OtherAction = new actions.OtherAction(99, null, 'Hello world');

    public doSomething() {
        this.dispatch(new actions.OtherAction(44));

        this.dispatch(new actions.OtherAction(58));

        this.dispatch(new actions.TestAction('My name'));
    }

    dispatch(action: Action) {

    }
}
