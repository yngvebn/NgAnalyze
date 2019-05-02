import { Action } from '@ngrx/store';
import { OtherAction } from './actions';

export class Service {

    public action: OtherAction = new OtherAction(99, null, 'Hello world');

    public doSomething() {
        this.dispatch(new OtherAction(44));
    }

    dispatch(action: Action) {

    }
}