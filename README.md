# Config-Validate

A .NET Global Tool designed to sniff out configuration issues between different environments.

## Conventions

This tool assumes a two things are true about your configuration files.

1. There is a core configuration, known as the _blueprint_. The _blueprint_ is normally `appSettings.json` and should have all, if not a large majority, of the keys that are part of your configuration.
1. There is a set of environment configurations. For example, your solution may have `appSettings.Development.json`, `appSettings.Staging.json`, and `appSettings.Production.json`. These are additive configurations to be layered on top of the _blueprint_.

If your solution doesn't follow this convention, then this tool will most likely not work for you.

## Running The Tool

From within the solution directory, first add the package.

```terminal
dotnet add package config-validate
```

Once installed, you can run the config-validate command from withing your project.

```terminal
/src/Host> dotnet config-validate
```

## Results

You first need to learn the vocabulary of `config-validate` used to understand the output.

### Key

The key is the string value used to access the configuration from within your .NET Core project.

For example, when using JSON as your configuration mechanism, you may get the following key.

```json
{
  "test": {
    "nested": "hello,world"
  }
}
```

With the key of `test:nested` and a value of `hello,world`.

### Missing

We mentioned that every project will have a _blueprint_ configuration. When a key is missing, it means that the key is found in the _blueprint_ but is not mentioned in the environment congifuration.

```javascript
// appSettings
// blueprint
{
    "test" : {
        "nested" : "hello,world"
    }
}
// production
{

}
```

If you were to run `config-validate` you would get the following results:

```terminal
-- Production
|     Key     | Missing | Unknown |    Value    |
| ----------- | ------- | ------- | ----------- |
| test:nested | Yes     | No      | hello,world |
```

### Unknown

Unknown keys are configuration settings found in environment configurations, but not found in the blueprint.

```javascript
// appSettings
// blueprint
{
    "test" : {
        "nested" : "hello,world"
    }
}
// production
{
    "what" : "new"
}
```

When running `config-validate` you would get the following results:

```terminal
-- Production
|     Key     | Missing | Unknown |    Value    |
| ----------- | ------- | ------- | ----------- |
| test:nested | Yes     | No      | hello,world |
| what        | No      | Yes     | new         |
```

### Value

The value column shows you the value of the setting when it is combined with the _blueprint_.

## Part Of The Build

The point of config-validate is to run during a CI/CD process to catch common mistakes. Because of that, the process will return exit codes that will fail most builds. We also understand that builds and configuration are complicated, so we've built some mechanism to ignore certain issues.

### Dot File Configuration

The `.config-validate` file should be placed in the project root and allows you to ignore certain keys, decide failure states, and enable artifacts

```javascript
{
    "config-validate-settings" : {
        "artificat" : {
            "enabled" : "true",
            "filename" : "config-validate.txt"
        },
        "fail-on" : "Unknown", //All, Missing, Unknown
        "ignore-keys" : [
            "data:protection",
            "my:ignored:key"
        ]
    }
}
```
