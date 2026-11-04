using HotChocolate.Execution.Configuration;

namespace ShiftWise.Api.GraphQL;

public static class GraphQLSetup
{
    // Shared by Program.cs and the tests so both run the exact same schema.
    public static IRequestExecutorBuilder AddShiftWiseGraphQL(this IServiceCollection services) =>
        services
            .AddGraphQLServer()
            .AddAuthorization()
            .AddQueryType<Query>()
            .AddMutationType<Mutation>()
            .AddErrorFilter<AppExceptionErrorFilter>()
            .ModifyRequestOptions(o => o.IncludeExceptionDetails = false)
            .AddMaxExecutionDepthRule(6);
}
