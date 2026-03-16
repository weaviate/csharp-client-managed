#!/bin/bash
# Integration Test Script for AI Product Search Demo
# Tests all major functionality of the WebApi and WebApp

set -e

echo "🧪 AI Product Search Integration Tests"
echo "======================================"
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test results
PASSED=0
FAILED=0

# Helper functions
pass() {
    echo -e "${GREEN}✓ PASS${NC}: $1"
    ((PASSED++))
}

fail() {
    echo -e "${RED}✗ FAIL${NC}: $1"
    ((FAILED++))
}

info() {
    echo -e "${YELLOW}ℹ INFO${NC}: $1"
}

# Check prerequisites
echo "📋 Checking prerequisites..."
echo ""

# Check Weaviate
if curl -s http://localhost:8080/v1/.well-known/ready > /dev/null 2>&1; then
    pass "Weaviate is running (port 8080)"
else
    fail "Weaviate is not running (port 8080)"
    echo "   Start with: ./ci/start_weaviate.sh"
fi

# Check Backend
if curl -s http://localhost:5001/health > /dev/null 2>&1; then
    pass "Backend API is running (port 5001)"
else
    fail "Backend API is not running (port 5001)"
    echo "   Start with: cd src/Example/WebApi && dotnet run"
fi

# Check Frontend
if curl -s http://localhost:5174 > /dev/null 2>&1; then
    pass "Frontend is running (port 5174)"
else
    # Try alternate port
    if curl -s http://localhost:5173 > /dev/null 2>&1; then
        pass "Frontend is running (port 5173)"
        FRONTEND_PORT=5173
    else
        fail "Frontend is not running"
        echo "   Start with: cd src/Example/WebApp && npm run dev"
        FRONTEND_PORT=5174
    fi
fi

FRONTEND_PORT=${FRONTEND_PORT:-5174}

echo ""
echo "🔍 Testing Backend API Endpoints..."
echo ""

# Test 1: Health endpoint
info "Testing health endpoint..."
HEALTH=$(curl -s http://localhost:5001/health)
if echo "$HEALTH" | grep -q "healthy"; then
    pass "Health endpoint returns healthy status"
else
    fail "Health endpoint did not return healthy"
fi

# Test 2: Get products
info "Testing get products endpoint..."
PRODUCTS=$(curl -s "http://localhost:5001/api/products?limit=5")
PRODUCT_COUNT=$(echo "$PRODUCTS" | python3 -c "import sys, json; print(len(json.load(sys.stdin)))" 2>/dev/null || echo "0")
if [ "$PRODUCT_COUNT" -ge 5 ]; then
    pass "Get products returns $PRODUCT_COUNT products"
else
    fail "Get products returned fewer than expected products"
fi

# Test 3: Get single product
info "Testing get single product..."
FIRST_UUID=$(echo "$PRODUCTS" | python3 -c "import sys, json; print(json.load(sys.stdin)[0]['uuid'])" 2>/dev/null || echo "")
if [ -n "$FIRST_UUID" ]; then
    PRODUCT=$(curl -s "http://localhost:5001/api/products/$FIRST_UUID")
    if echo "$PRODUCT" | grep -q "$FIRST_UUID"; then
        pass "Get single product returns correct product"
    else
        fail "Get single product did not return expected product"
    fi
else
    fail "Could not extract UUID from products response"
fi

# Test 4: Semantic search
info "Testing semantic search..."
SEARCH_RESULT=$(curl -s -X POST http://localhost:5001/api/search \
    -H "Content-Type: application/json" \
    -d '{"query":"laptop for work","mode":"semantic","limit":5}')
SEARCH_COUNT=$(echo "$SEARCH_RESULT" | python3 -c "import sys, json; print(len(json.load(sys.stdin)))" 2>/dev/null || echo "0")
if [ "$SEARCH_COUNT" -gt 0 ]; then
    pass "Semantic search returns $SEARCH_COUNT results"

    # Check for distance metadata
    HAS_DISTANCE=$(echo "$SEARCH_RESULT" | python3 -c "import sys, json; r=json.load(sys.stdin); print('distance' in r[0].get('metadata', {}))" 2>/dev/null || echo "False")
    if [ "$HAS_DISTANCE" = "True" ]; then
        pass "Search results include distance metadata"
    else
        fail "Search results missing distance metadata"
    fi
else
    fail "Semantic search returned no results"
fi

# Test 5: Hybrid search
info "Testing hybrid search..."
HYBRID_RESULT=$(curl -s -X POST http://localhost:5001/api/search \
    -H "Content-Type: application/json" \
    -d '{"query":"wireless headphones","mode":"hybrid","limit":5}')
HYBRID_COUNT=$(echo "$HYBRID_RESULT" | python3 -c "import sys, json; print(len(json.load(sys.stdin)))" 2>/dev/null || echo "0")
if [ "$HYBRID_COUNT" -gt 0 ]; then
    pass "Hybrid search returns $HYBRID_COUNT results"
else
    fail "Hybrid search returned no results"
fi

# Test 6: Keyword search (BM25)
info "Testing keyword search (BM25)..."
KEYWORD_RESULT=$(curl -s -X POST http://localhost:5001/api/search \
    -H "Content-Type: application/json" \
    -d '{"query":"laptop","mode":"keyword","limit":5}')
KEYWORD_COUNT=$(echo "$KEYWORD_RESULT" | python3 -c "import sys, json; print(len(json.load(sys.stdin)))" 2>/dev/null || echo "0")
if [ "$KEYWORD_COUNT" -gt 0 ]; then
    pass "Keyword search returns $KEYWORD_COUNT results"
else
    fail "Keyword search returned no results"
fi

# Test 7: Similar products (NearObject)
if [ -n "$FIRST_UUID" ]; then
    info "Testing similar products..."
    SIMILAR=$(curl -s "http://localhost:5001/api/products/$FIRST_UUID/similar?limit=4")
    SIMILAR_COUNT=$(echo "$SIMILAR" | python3 -c "import sys, json; print(len(json.load(sys.stdin)))" 2>/dev/null || echo "0")
    if [ "$SIMILAR_COUNT" -gt 0 ]; then
        pass "Similar products returns $SIMILAR_COUNT recommendations"
    else
        fail "Similar products returned no results"
    fi
fi

# Test 8: Facets
info "Testing facets endpoint..."
FACETS=$(curl -s http://localhost:5001/api/products/facets)
CAT_COUNT=$(echo "$FACETS" | python3 -c "import sys, json; print(len(json.load(sys.stdin).get('categories', [])))" 2>/dev/null || echo "0")
if [ "$CAT_COUNT" -gt 0 ]; then
    pass "Facets endpoint returns $CAT_COUNT categories"
else
    fail "Facets endpoint returned no categories"
fi

# Test 9: Filtered search
info "Testing filtered search..."
FILTERED=$(curl -s -X POST http://localhost:5001/api/search \
    -H "Content-Type: application/json" \
    -d '{"query":"electronics","mode":"semantic","limit":20,"filters":{"category":"Electronics","minPrice":50,"maxPrice":200}}')
FILTERED_COUNT=$(echo "$FILTERED" | python3 -c "import sys, json; print(len(json.load(sys.stdin)))" 2>/dev/null || echo "0")
if [ "$FILTERED_COUNT" -ge 0 ]; then
    pass "Filtered search executed successfully ($FILTERED_COUNT results)"

    # Verify all results are in Electronics category
    ALL_ELECTRONICS=$(echo "$FILTERED" | python3 -c "import sys, json; r=json.load(sys.stdin); print(all(x['object']['category']=='Electronics' for x in r))" 2>/dev/null || echo "True")
    if [ "$ALL_ELECTRONICS" = "True" ]; then
        pass "All filtered results match category filter"
    else
        fail "Some results don't match category filter"
    fi
else
    fail "Filtered search failed"
fi

echo ""
echo "🖥️  Testing Frontend Pages..."
echo ""

# Test 10: Home page
info "Testing home page..."
HOME_PAGE=$(curl -s http://localhost:$FRONTEND_PORT/)
if echo "$HOME_PAGE" | grep -q "AI-Powered Product Search"; then
    pass "Home page loads with correct title"
else
    fail "Home page did not load correctly"
fi

# Test 11: Search page
info "Testing search page..."
SEARCH_PAGE=$(curl -s "http://localhost:$FRONTEND_PORT/search?q=laptop&mode=semantic")
if echo "$SEARCH_PAGE" | grep -q "Search - AI Product Search"; then
    pass "Search page loads correctly"
else
    fail "Search page did not load correctly"
fi

# Test 12: Product detail page
if [ -n "$FIRST_UUID" ]; then
    info "Testing product detail page..."
    PRODUCT_PAGE=$(curl -s "http://localhost:$FRONTEND_PORT/product/$FIRST_UUID")
    if echo "$PRODUCT_PAGE" | grep -q "AI Product Search"; then
        pass "Product detail page loads correctly"
    else
        fail "Product detail page did not load correctly"
    fi
fi

echo ""
echo "📊 Test Results Summary"
echo "======================"
echo ""
echo -e "${GREEN}Passed: $PASSED${NC}"
echo -e "${RED}Failed: $FAILED${NC}"
echo ""

if [ $FAILED -eq 0 ]; then
    echo -e "${GREEN}🎉 All tests passed!${NC}"
    exit 0
else
    echo -e "${RED}❌ Some tests failed${NC}"
    exit 1
fi
