package com.trivium.orderworker;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.client.JdkClientHttpRequestFactory;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;
import org.springframework.web.client.RestTemplate;

import java.net.http.HttpClient;
import java.util.Map;

@Component
public class OrderConsumer {

    private static final Logger logger = LoggerFactory.getLogger(OrderConsumer.class);
    private final ObjectMapper mapper = new ObjectMapper();
    private final RestTemplate rest = new RestTemplate(
        new JdkClientHttpRequestFactory(HttpClient.newHttpClient()));

    @Value("${order-worker.order-api-url}")
    private String orderApiUrl;

    @Value("${order-worker.notifications-url}")
    private String notificationsUrl;

    @RabbitListener(queues = "orders")
    public void onOrder(String message) {
        Integer orderId = null;
        try {
            JsonNode order = mapper.readTree(message);
            JsonNode idNode = order.get("id");
            if (idNode == null) {
                idNode = order.get("Id");
            }
            if (idNode == null || !idNode.canConvertToInt()) {
                throw new IllegalArgumentException("Order message does not contain a valid id");
            }
            orderId = idNode.asInt();
            logger.info("order_consumed orderId={} status=Pending", orderId);

            updateStatus(orderId, "Processing");
            notify(orderId, "Processing");
            logger.info("order_processing orderId={} status=Processing", orderId);

            Thread.sleep(1500); // simulate work: pack, ship, etc.

            updateStatus(orderId, "Shipped");
            notify(orderId, "Shipped");
            logger.info("order_shipped orderId={} status=Shipped", orderId);
        } catch (Exception e) {
            logger.error("order_processing_failed orderId={} error={}", orderId, e.getMessage(), e);
        }
    }

    private void updateStatus(int id, String status) {
        rest.patchForObject(orderApiUrl + "/orders/{id}/status", Map.of("status", status), Void.class, id);
    }

    private void notify(int id, String status) {
        rest.postForObject(notificationsUrl + "/notify", Map.of("orderId", id, "status", status), Void.class);
    }
}
