import { TriviaApp } from '../../src/trivia-frontend/src/trivia-app';
import { createFixture } from '@aurelia/testing';

describe('trivia-app', () => {
  it('should render message', async () => {
    const { assertText } = await createFixture(
      '<trivia-app></trivia-app>',
      {},
      [TriviaApp],
    ).started;

    assertText('Trivia Battle', { compact: true });
  });

});
