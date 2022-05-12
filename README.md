# Event Driven Serverless CDK

This project contains an example of building an AWS native, event driven, customer review analysis application. It uses serverless components and native AWS service integrations. The application is deployed using the AWS CDK, written in C#.

## Architecture

![](./assets/architecture.PNG)

The application consists of 6 services:

### API

Receives requests from a front-end application.

### Sentiment Analysis

Service to analyze the review content and detect the sentiment.

### Notification Service

Sends email notifications back to the customer

### Customer Contact Service

Negative reviews are followed up by a customer service representitive. This service manages that customer service flow.


### CRM service

Simple API to retrieve customer information for personalised email communication.

### Event History Service

An audit service, to store all events relating to a given review.

## Deployment

The entire application can be deployed by running the below command from the root directory.

```
cdk deploy
```