import { Action } from '@ngrx/store';
import * as actions from './actions';
import { NewAction as newAction } from './actions2';

export class Service {

    public action: actions.OtherAction = new actions.OtherAction(99, null, 'Hello world');
    // modifier identifier: QualifiedName = new PropertyAccessExpression
    public doSomething() {
        this.dispatch(new actions.OtherAction(44));

        this.dispatch(new actions.OtherAction(58));

        this.dispatch(new actions.TestAction('My name'));

        this.dispatch(new newAction('Hello'));
    }

    dispatch(action: Action) {

    }
}