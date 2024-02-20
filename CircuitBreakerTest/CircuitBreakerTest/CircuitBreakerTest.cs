using System.Runtime.CompilerServices;
using FluentAssertions;
using Polly;
using Polly.CircuitBreaker;
namespace CircuitBreakerTest;

[TestFixture]
public class CircuitBreakerTest
{
    [Test]
    public async Task TestResiliencePipelineDirectly()
    {
        var resetTimeout = TimeSpan.FromSeconds(2);
        var stateProvider = new CircuitBreakerStateProvider();
        var builder = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions()
            {
                BreakDuration = resetTimeout,
                SamplingDuration = TimeSpan.FromSeconds(10),
                FailureRatio = 0.1,
                StateProvider = stateProvider,
                MinimumThroughput = 2,
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
                OnOpened = arguments =>
                {
                    Console.WriteLine("OnOpened <=============================");
                    return ValueTask.CompletedTask;
                },
                OnClosed = arguments =>
                {
                    Console.WriteLine("OnClosed <=============================");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = arguments =>
                {
                    Console.WriteLine("OnHalfOpened <=============================");
                    return ValueTask.CompletedTask;
                },
            });
        var circuitBreaker = builder.Build();

        circuitBreaker.Execute(() => true);

        // operate, need 2 calls in sampling timeframe to trigger error; we have 1 success and 1 error after this call = failure ration 0.5, minimum samples reached
        var errorAction = () => circuitBreaker.Execute(() => throw new InvalidOperationException());
        errorAction.Should().Throw<InvalidOperationException>();

        // operate
        Action act = () => circuitBreaker.Execute(() => true);
        act.Should().Throw<BrokenCircuitException>();
        await Task.Delay(1.5 * resetTimeout); // next call should transition to half-open
        try
        {
            await circuitBreaker.ExecuteAsync(async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                throw new InvalidOperationException();
            });
        }
        catch (InvalidOperationException)
        {
        }

        stateProvider.CircuitState.Should().Be(CircuitState.Open);

        await Task.Delay(resetTimeout * 3);

        await circuitBreaker.ExecuteAsync(_ => ValueTask.CompletedTask);
        stateProvider.CircuitState.Should().Be(CircuitState.Closed);
    }

    [Test]
    public async Task TestResiliencePipelineDirectly2()
    {
        var resetTimeout = TimeSpan.FromSeconds(2);
        var stateProvider = new CircuitBreakerStateProvider();
        var builder = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions()
            {
                BreakDuration = resetTimeout,
                SamplingDuration = TimeSpan.FromSeconds(10),
                FailureRatio = 0.1,
                StateProvider = stateProvider,
                MinimumThroughput = 2,
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
                OnOpened = arguments =>
                {
                    Console.WriteLine("OnOpened <=============================");
                    return ValueTask.CompletedTask;
                },
                OnClosed = arguments =>
                {
                    Console.WriteLine("OnClosed <=============================");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = arguments =>
                {
                    Console.WriteLine("OnHalfOpened <=============================");
                    return ValueTask.CompletedTask;
                },
            });
        var circuitBreaker = builder.Build();
        
        circuitBreaker.Execute(() => true);

        // operate, need 2 calls in sampling timeframe to trigger error; we have 1 success and 1 error after this call = failure ration 0.5, minimum samples reached
        var errorAction = () => circuitBreaker.Execute(() => throw new InvalidOperationException());
        errorAction.Should().Throw<InvalidOperationException>();

        // operate
        Console.WriteLine("Expecting OPEN state");
        Action act = () => circuitBreaker.Execute(() => true);
        act.Should().Throw<BrokenCircuitException>();
        stateProvider.CircuitState.Should().Be(CircuitState.Open);
        
        await Task.Delay(1.5 * resetTimeout); // next call should transition to half-open
        try
        {
            await circuitBreaker.ExecuteAsync(async ct =>
            {
                Console.WriteLine("Expecting HALFOPEN state");
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
                throw new MyUnhandledException();
            });
        }
        catch (MyUnhandledException)
        {
        }

        Console.WriteLine("Expecting HALFOPEN state");
        stateProvider.CircuitState.Should().Be(CircuitState.HalfOpen);

        await Task.Delay(resetTimeout * 1.5);

        try
        {
            await circuitBreaker.ExecuteAsync(_ => ValueTask.CompletedTask);
        }
        catch (BrokenCircuitException)
        {
            Console.WriteLine($"Bad CircuitState: {stateProvider.CircuitState}");
        }
        stateProvider.CircuitState.Should().Be(CircuitState.Closed);
    }
}