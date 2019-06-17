import defaultAction, { TestAction } from './actions';
import { Service } from './service1';

export class OtherService {
    public dispatchSomething() {
        new Service().dispatch(new defaultAction('Hello world!'));
        new Service().dispatch(new TestAction('Hello world!'));
                                // NewExpression - (Identifier, StringLiteral)
        myFunction('Hello world!');
    }

}
export const myFunction = (str: string) => {

}