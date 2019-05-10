import { otherAction } from './actions'
import { Service } from './service1';

export class OtherService {
    public dispatchSomething() {
        new Service().dispatch(otherAction(
            33
        ));

        myFunction('Hello world!');
    }

}
export const myFunction = (str: string) => {

}