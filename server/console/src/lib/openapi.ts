export type HttpMethod = "get" | "post" | "put" | "patch" | "delete";

const HTTP_METHODS: HttpMethod[] = ["get", "post", "put", "patch", "delete"];
const JSON_CONTENT_TYPES = ["application/json", "application/*+json"];

type Primitive = string | number | boolean | null;

export interface OpenApiSchema {
  type?: string;
  title?: string;
  description?: string;
  properties?: Record<string, OpenApiSchema>;
  items?: OpenApiSchema;
  required?: string[];
  enum?: Primitive[];
  anyOf?: OpenApiSchema[];
  oneOf?: OpenApiSchema[];
  allOf?: OpenApiSchema[];
  additionalProperties?: boolean | OpenApiSchema;
  default?: unknown;
  example?: unknown;
  $ref?: string;
}

interface OpenApiMediaType {
  schema?: OpenApiSchema;
}

interface OpenApiRequestBody {
  required?: boolean;
  content?: Record<string, OpenApiMediaType>;
}

interface OpenApiResponse {
  description?: string;
  content?: Record<string, OpenApiMediaType>;
}

interface OpenApiParameter {
  name: string;
  in: "path" | "query" | "header" | "cookie";
  required?: boolean;
  description?: string;
  schema?: OpenApiSchema;
}

interface OpenApiOperation {
  summary?: string;
  operationId?: string;
  tags?: string[];
  parameters?: OpenApiParameter[];
  requestBody?: OpenApiRequestBody;
  responses?: Record<string, OpenApiResponse>;
}

interface OpenApiPathItem {
  parameters?: OpenApiParameter[];
  get?: OpenApiOperation;
  post?: OpenApiOperation;
  put?: OpenApiOperation;
  patch?: OpenApiOperation;
  delete?: OpenApiOperation;
}

export interface OpenApiDocument {
  openapi?: string;
  info?: { title?: string; version?: string };
  paths?: Record<string, OpenApiPathItem>;
  components?: {
    schemas?: Record<string, OpenApiSchema>;
  };
}

export interface OperationParameter {
  name: string;
  in: "path" | "query" | "header" | "cookie";
  required: boolean;
  description?: string;
  schema?: OpenApiSchema;
}

export interface ConsoleOperation {
  key: string;
  method: HttpMethod;
  path: string;
  summary: string;
  operationId?: string;
  tags: string[];
  parameters: OperationParameter[];
  requestBodySchema?: OpenApiSchema;
  requestBodyRequired: boolean;
  dataSchema?: OpenApiSchema;
  expectedFields: string[];
}

export interface LoadedOpenApi {
  sourceUrl: string;
  document: OpenApiDocument;
}

export interface ExecuteArgs {
  operation: ConsoleOperation;
  apiBaseUrl: string;
  bearerToken?: string;
  pathValues: Record<string, string>;
  queryValues: Record<string, string>;
  bodyText?: string;
}

export interface ExecuteResult {
  status: number;
  data: unknown;
}

function uniq(values: string[]): string[] {
  return Array.from(new Set(values));
}

function getRefValue<T>(document: OpenApiDocument, ref: string): T | undefined {
  if (!ref.startsWith("#/")) return undefined;

  const keys = ref
    .slice(2)
    .split("/")
    .map((segment) => segment.replace(/~1/g, "/").replace(/~0/g, "~"));

  let cursor: unknown = document;
  for (const key of keys) {
    if (!cursor || typeof cursor !== "object") return undefined;
    cursor = (cursor as Record<string, unknown>)[key];
  }

  return cursor as T | undefined;
}

function mergeSchemas(base: OpenApiSchema, incoming: OpenApiSchema): OpenApiSchema {
  const merged: OpenApiSchema = {
    ...base,
    ...incoming,
  };

  if (base.properties || incoming.properties) {
    merged.properties = {
      ...(base.properties ?? {}),
      ...(incoming.properties ?? {}),
    };
  }

  if (base.required || incoming.required) {
    merged.required = uniq([...(base.required ?? []), ...(incoming.required ?? [])]);
  }

  if (!merged.items) {
    merged.items = base.items ?? incoming.items;
  }

  if (!merged.type) {
    merged.type = base.type ?? incoming.type;
  }

  return merged;
}

export function resolveSchema(
  document: OpenApiDocument,
  schema?: OpenApiSchema,
  seenRefs: Set<string> = new Set(),
): OpenApiSchema | undefined {
  if (!schema) return undefined;

  if (schema.$ref) {
    if (seenRefs.has(schema.$ref)) return undefined;
    seenRefs.add(schema.$ref);
    const referenced = getRefValue<OpenApiSchema>(document, schema.$ref);
    return resolveSchema(document, referenced, seenRefs) ?? referenced;
  }

  if (schema.allOf?.length) {
    let merged: OpenApiSchema = {};
    for (const part of schema.allOf) {
      const resolvedPart = resolveSchema(document, part, seenRefs);
      if (resolvedPart) {
        merged = mergeSchemas(merged, resolvedPart);
      }
    }
    return mergeSchemas(merged, schema);
  }

  if (schema.anyOf?.length) {
    return resolveSchema(document, schema.anyOf[0], seenRefs) ?? schema.anyOf[0];
  }

  if (schema.oneOf?.length) {
    return resolveSchema(document, schema.oneOf[0], seenRefs) ?? schema.oneOf[0];
  }

  return schema;
}

function selectJsonSchema(content?: Record<string, OpenApiMediaType>): OpenApiSchema | undefined {
  if (!content) return undefined;

  for (const contentType of JSON_CONTENT_TYPES) {
    const schema = content[contentType]?.schema;
    if (schema) return schema;
  }

  for (const [contentType, mediaType] of Object.entries(content)) {
    if (contentType.toLowerCase().includes("json") && mediaType.schema) {
      return mediaType.schema;
    }
  }

  return Object.values(content).find((item) => Boolean(item.schema))?.schema;
}

function selectResponseSchema(operation: OpenApiOperation): OpenApiSchema | undefined {
  const responses = operation.responses ?? {};
  const statuses = [
    "200",
    "201",
    "202",
    "default",
    ...Object.keys(responses).filter((key) => key.startsWith("2")),
    ...Object.keys(responses),
  ];

  for (const status of statuses) {
    const schema = selectJsonSchema(responses[status]?.content);
    if (schema) return schema;
  }

  return undefined;
}

function selectDataSchema(document: OpenApiDocument, responseSchema?: OpenApiSchema): OpenApiSchema | undefined {
  const resolved = resolveSchema(document, responseSchema);
  if (!resolved) return undefined;

  if (resolved.type === "array" || resolved.items) {
    return resolveSchema(document, resolved.items);
  }

  const itemsSchema = resolved.properties?.items;
  if (itemsSchema) {
    const resolvedItems = resolveSchema(document, itemsSchema);
    if (resolvedItems?.type === "array" || resolvedItems?.items) {
      return resolveSchema(document, resolvedItems.items);
    }
  }

  const dataSchema = resolved.properties?.data;
  if (dataSchema) {
    const resolvedData = resolveSchema(document, dataSchema);
    if (resolvedData?.type === "array" || resolvedData?.items) {
      return resolveSchema(document, resolvedData.items);
    }
    return resolvedData;
  }

  return resolved;
}

function getExpectedFields(document: OpenApiDocument, schema?: OpenApiSchema): string[] {
  const resolved = resolveSchema(document, schema);
  if (!resolved) return [];

  if (resolved.type === "array" || resolved.items) {
    return getExpectedFields(document, resolved.items);
  }

  return Object.keys(resolved.properties ?? {});
}

function combineParameters(pathItem: OpenApiPathItem, operation: OpenApiOperation): OperationParameter[] {
  const combined = [...(pathItem.parameters ?? []), ...(operation.parameters ?? [])];
  const output: OperationParameter[] = [];
  const seen = new Set<string>();

  for (const parameter of combined) {
    const key = `${parameter.in}:${parameter.name}`;
    if (seen.has(key)) continue;
    seen.add(key);
    output.push({
      name: parameter.name,
      in: parameter.in,
      required: Boolean(parameter.required || parameter.in === "path"),
      description: parameter.description,
      schema: parameter.schema,
    });
  }

  return output;
}

export function extractConsoleOperations(
  document: OpenApiDocument,
  pathPrefix: string = "/console/ui/",
): ConsoleOperation[] {
  const paths = document.paths ?? {};
  const output: ConsoleOperation[] = [];

  for (const path of Object.keys(paths).sort()) {
    if (!path.startsWith(pathPrefix)) continue;

    const pathItem = paths[path];
    if (!pathItem) continue;

    for (const method of HTTP_METHODS) {
      const operation = pathItem[method];
      if (!operation) continue;

      const responseSchema = selectResponseSchema(operation);
      const dataSchema = selectDataSchema(document, responseSchema);
      const expectedFields = getExpectedFields(document, dataSchema);

      output.push({
        key: `${method.toUpperCase()} ${path}`,
        method,
        path,
        summary: operation.summary ?? operation.operationId ?? `${method.toUpperCase()} ${path}`,
        operationId: operation.operationId,
        tags: operation.tags ?? [],
        parameters: combineParameters(pathItem, operation),
        requestBodySchema: selectJsonSchema(operation.requestBody?.content),
        requestBodyRequired: Boolean(operation.requestBody?.required),
        dataSchema,
        expectedFields,
      });
    }
  }

  return output;
}

function normalizeBaseUrl(value: string): string {
  return value.replace(/\/+$/, "");
}

function applyPathValues(path: string, pathValues: Record<string, string>): string {
  return path.replace(/\{([^}]+)\}/g, (_whole, key: string) => {
    const value = pathValues[key];
    return value ? encodeURIComponent(value) : `{${key}}`;
  });
}

function appendQuery(url: URL, queryValues: Record<string, string>) {
  for (const [key, value] of Object.entries(queryValues)) {
    if (!value) continue;
    url.searchParams.set(key, value);
  }
}

export async function loadOpenApi(sources: string[]): Promise<LoadedOpenApi> {
  const errors: string[] = [];

  for (const sourceUrl of sources) {
    try {
      const response = await fetch(sourceUrl, {
        headers: { Accept: "application/json" },
      });
      if (!response.ok) {
        errors.push(`${sourceUrl} -> HTTP ${response.status}`);
        continue;
      }

      const document = (await response.json()) as OpenApiDocument;
      if (!document.paths) {
        errors.push(`${sourceUrl} -> invalid OpenAPI payload`);
        continue;
      }

      return { sourceUrl, document };
    } catch (error) {
      errors.push(`${sourceUrl} -> ${error instanceof Error ? error.message : String(error)}`);
    }
  }

  throw new Error(`OpenAPI konnte nicht geladen werden: ${errors.join(" | ")}`);
}

export function initialValuesForLocation(
  parameters: OperationParameter[],
  location: OperationParameter["in"],
): Record<string, string> {
  const values: Record<string, string> = {};

  for (const parameter of parameters) {
    if (parameter.in !== location) continue;
    const defaultValue = parameter.schema?.default;
    if (
      typeof defaultValue === "string" ||
      typeof defaultValue === "number" ||
      typeof defaultValue === "boolean"
    ) {
      values[parameter.name] = String(defaultValue);
    } else {
      values[parameter.name] = "";
    }
  }

  return values;
}

export function createJsonTemplate(
  document: OpenApiDocument,
  schema?: OpenApiSchema,
  depth: number = 0,
): unknown {
  if (depth > 5) return null;

  const resolved = resolveSchema(document, schema);
  if (!resolved) return null;

  if (resolved.example !== undefined) return resolved.example;
  if (resolved.default !== undefined) return resolved.default;
  if (resolved.enum?.length) return resolved.enum[0];

  if ((resolved.type === "array" || resolved.items) && resolved.items) {
    return [createJsonTemplate(document, resolved.items, depth + 1)];
  }

  if (resolved.type === "object" || resolved.properties) {
    const out: Record<string, unknown> = {};
    const properties = resolved.properties ?? {};
    const includedFields = resolved.required?.length ? resolved.required : Object.keys(properties);

    for (const field of includedFields) {
      out[field] = createJsonTemplate(document, properties[field], depth + 1);
    }

    return out;
  }

  if (resolved.type === "integer" || resolved.type === "number") return 0;
  if (resolved.type === "boolean") return false;
  if (resolved.type === "string") return "";

  return null;
}

export async function executeOperation(args: ExecuteArgs): Promise<ExecuteResult> {
  const method = args.operation.method.toUpperCase();
  const path = applyPathValues(args.operation.path, args.pathValues);
  const baseUrl = normalizeBaseUrl(args.apiBaseUrl);
  const url = new URL(`${baseUrl}${path.startsWith("/") ? path : `/${path}`}`);

  appendQuery(url, args.queryValues);

  const headers: Record<string, string> = {
    Accept: "application/json",
  };

  if (args.bearerToken?.trim()) {
    headers.Authorization = `Bearer ${args.bearerToken.trim()}`;
  }

  let body: string | undefined;
  if (["POST", "PUT", "PATCH", "DELETE"].includes(method)) {
    if (args.bodyText?.trim()) {
      try {
        body = JSON.stringify(JSON.parse(args.bodyText));
      } catch {
        throw new Error("Request-Body ist kein valides JSON.");
      }
      headers["Content-Type"] = "application/json";
    } else if (args.operation.requestBodyRequired) {
      throw new Error("Dieser Endpoint erwartet einen Request-Body.");
    }
  }

  const response = await fetch(url.toString(), {
    method,
    headers,
    body,
  });

  const contentType = response.headers.get("content-type")?.toLowerCase() ?? "";
  let data: unknown = null;
  if (response.status !== 204) {
    data = contentType.includes("json") ? await response.json() : await response.text();
  }

  if (!response.ok) {
    const details = typeof data === "string" ? data : JSON.stringify(data);
    throw new Error(`API ${response.status}: ${details}`);
  }

  return {
    status: response.status,
    data,
  };
}
